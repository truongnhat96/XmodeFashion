using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Data.Fashion;

public sealed class FashionPayments(FashionContext db, IFashionPaymentGateway gateway) : IFashionPayments
{
    // Lock the order first in every payment path. Network creation uses the same persisted
    // reference on every retry; a timed-out request must never generate a second charge.
    private Task<FashionOrder?> LockedOrder(Guid id, CancellationToken ct) => db.Orders
        .FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct);

    public Task<PaymentLink> StartAsync(CartOwner owner, Guid orderId, string ipAddress, CancellationToken ct = default)
    {
        FashionRules.ValidateOwner(owner);
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var order = await LockedOrder(orderId, ct);
            if (order is null || !(owner.UserId is int uid ? order.UserId == uid : order.UserId is null && order.GuestKeyHash == owner.GuestKeyHash))
                throw new CatalogNotFoundException("Đơn hàng không tồn tại.");
            if (order.PaymentMethod == "COD") throw new CatalogValidationException("Đơn này sử dụng COD.");
            if (order.PaymentStatus != "Pending" || order.Status != "Pending")
                return new PaymentLink(null, order.PaymentStatus, "Đơn hàng không còn chờ thanh toán.");
            if (order.PaymentExpiresAt <= DateTime.UtcNow)
            {
                await CancelUnpaid(order, "Expired", "Hết thời gian thanh toán online", ct);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                return new PaymentLink(null, "Expired", "Đã hết thời gian thanh toán. Tồn kho đã được trả lại.");
            }
            if (!gateway.Methods().Any(m => m.Code == order.PaymentMethod && m.Available))
                throw new PaymentGatewayException("Cổng thanh toán đang tạm ngưng hoặc chưa được cấu hình.");
            var payment = await db.Payments.SingleAsync(p => p.OrderId == order.Id, ct);
            if (payment.Url is null)
            {
                payment.Url = await gateway.CreateAsync(new(payment.Id, order.Id, order.Code, payment.Provider, payment.Amount, payment.ExpiresAt), ipAddress, ct);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return new PaymentLink(payment.Url, order.PaymentStatus);
        });
    }

    public Task<PaymentReceipt> NotifyAsync(PaymentNotification n, CancellationToken ct = default) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await LockedOrder(n.Reference, ct); // Payment Id is the order Id, persisted when placing the order.
        if (order is null || order.PaymentMethod != n.Provider) return new PaymentReceipt("01");
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == n.Reference && p.Provider == n.Provider, ct);
        if (payment is null) return new PaymentReceipt("01");
        if (n.Amount != payment.Amount || n.Amount != order.Total) return new PaymentReceipt("04");
        if (payment.Status == "Paid") return new PaymentReceipt("02", order.Id);
        if (n.Pending) return new PaymentReceipt("00", order.Id);
        if (n.Success)
        {
            if (string.IsNullOrWhiteSpace(n.TransactionId) || n.TransactionId.Length > 100 || n.TransactionId == "0") return new PaymentReceipt("97");
            if (await db.Payments.AnyAsync(p => p.Provider == n.Provider && p.TransactionId == n.TransactionId && p.Id != payment.Id, ct)) return new PaymentReceipt("97");
            payment.Status = "Paid"; payment.TransactionId = n.TransactionId;
            order.PaidAt = DateTime.UtcNow;
            // A late success must never revive a cancelled order or re-reserve sold stock.
            order.PaymentStatus = order.Status == "Cancelled" ? "RefundRequired" : "Paid";
            order.History.Add(new FashionOrderStatusHistory { PreviousStatus = order.Status, Status = order.Status,
                Reason = order.Status == "Cancelled" ? "Nhận tiền sau khi hủy đơn; cần hoàn tiền" : $"Đã nhận thanh toán {n.Provider}" });
        }
        else if (order.PaymentStatus == "Pending") await CancelUnpaid(order, "Failed", "Cổng thanh toán xác nhận giao dịch không thành công", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new PaymentReceipt("00", order.Id);
    });

    public async Task<int> ExpireAsync(CancellationToken ct = default)
    {
        var ids = await db.Orders.AsNoTracking().Where(o => o.PaymentStatus == "Pending" && o.PaymentExpiresAt < DateTime.UtcNow)
            .OrderBy(o => o.PaymentExpiresAt).Select(o => o.Id).Take(100).ToListAsync(ct);
        foreach (var id in ids)
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var o = await LockedOrder(id, ct);
                if (o is not null && o.PaymentStatus == "Pending" && o.PaymentExpiresAt < DateTime.UtcNow)
                    await CancelUnpaid(o, "Expired", "Hết thời gian thanh toán online", ct);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            });
        return ids.Count;
    }

    private async Task CancelUnpaid(FashionOrder order, string paymentStatus, string reason, CancellationToken ct)
    {
        if (order.Status == "Pending")
        {
            var items = await db.OrderItems.Where(i => i.OrderId == order.Id).Include(i => i.Variant).OrderBy(i => i.VariantId).ToListAsync(ct);
            foreach (var i in items) i.Variant.Stock += i.Quantity;
            order.History.Add(new FashionOrderStatusHistory { PreviousStatus = order.Status, Status = "Cancelled", Reason = reason });
            order.Status = "Cancelled";
        }
        order.PaymentStatus = paymentStatus;
        var payment = await db.Payments.SingleAsync(p => p.OrderId == order.Id, ct); payment.Status = paymentStatus;
    }

    public Task MarkRefundedAsync(Guid id, byte[] version, string reference, int? actor, CancellationToken ct = default) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 200 || version is null) throw new CatalogValidationException("Cần mã tham chiếu hoàn tiền (tối đa 200 ký tự).");
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var o = await LockedOrder(id, ct) ?? throw new CatalogNotFoundException("Đơn hàng không tồn tại.");
        if (!o.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException();
        if (o.Status != "Cancelled" || o.PaymentStatus != "RefundRequired") throw new CatalogValidationException("Đơn không thuộc diện cần hoàn tiền.");
        o.PaymentStatus = "Refunded";
        o.History.Add(new FashionOrderStatusHistory { PreviousStatus = o.Status, Status = o.Status, ActorUserId = actor,
            Reason = "Admin xác nhận đã hoàn tiền bên ngoài hệ thống; tham chiếu: " + reference.Trim() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    });
}
