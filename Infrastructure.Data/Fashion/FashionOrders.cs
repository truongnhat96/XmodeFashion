using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Data.Fashion;

public sealed class FashionOrders(FashionContext db) : IFashionOrders
{
    public async Task<CartView> GetCartAsync(CartOwner owner, CancellationToken ct = default)
    {
        FashionRules.ValidateOwner(owner);
        var cart = await CartQuery(owner).AsNoTracking().Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .ThenInclude(p => p.Category).Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .ThenInclude(p => p.Images).ThenInclude(i => i.MediaAsset).AsSplitQuery().SingleOrDefaultAsync(ct);
        var items = cart?.Items.Select(i => new CartLineView(i.Id, i.VariantId, i.Variant.ProductId, i.Variant.Product.Name,
            i.Variant.Product.Images.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.SortOrder).FirstOrDefault()?.MediaAsset.Url, i.Variant.Sku,
            i.Variant.Color, i.Variant.Size, i.Variant.Product.Price, i.Quantity, i.Variant.Stock,
            i.Variant.IsActive && i.Variant.Product.IsVisible && !i.Variant.Product.IsDeleted && i.Variant.Product.Category.IsVisible)).ToList() ?? [];
        var fee = await db.StoreSettings.AsNoTracking().Where(s => s.Id == 1).Select(s => s.ShippingFee).SingleOrDefaultAsync(ct);
        return new(items, items.Sum(i => i.Price * i.Quantity), fee);
    }

    public async Task SetCartItemAsync(CartOwner owner, int variantId, int quantity, CancellationToken ct = default)
    {
        FashionRules.ValidateOwner(owner);
        if (quantity <= 0) throw new CatalogValidationException("Số lượng phải lớn hơn 0.");
        var variant = await db.ProductVariants.Include(v => v.Product).ThenInclude(p => p.Category).SingleOrDefaultAsync(v => v.Id == variantId, ct);
        if (variant is null || !variant.IsActive || !variant.Product.IsVisible || variant.Product.IsDeleted || !variant.Product.Category.IsVisible)
            throw new CatalogValidationException("Sản phẩm không còn được bán.");
        if (quantity > variant.Stock) throw new CatalogValidationException("Số lượng vượt tồn kho.");
        var cart = await CartQuery(owner).Include(c => c.Items).SingleOrDefaultAsync(ct);
        if (cart is null)
        {
            cart = new FashionCart { Id = Guid.NewGuid(), UserId = owner.UserId, GuestKeyHash = owner.GuestKeyHash };
            db.Carts.Add(cart);
        }
        var line = cart.Items.SingleOrDefault(i => i.VariantId == variantId);
        if (line is null)
        {
            line = new FashionCartItem { Id = Guid.NewGuid(), CartId = cart.Id, VariantId = variantId, Quantity = quantity };
            db.CartItems.Add(line);
        }
        else line.Quantity = quantity;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveCartItemAsync(CartOwner owner, Guid itemId, CancellationToken ct = default)
    {
        FashionRules.ValidateOwner(owner);
        var cart = await CartQuery(owner).Include(c => c.Items).SingleOrDefaultAsync(ct);
        var line = cart?.Items.SingleOrDefault(i => i.Id == itemId) ?? throw new CatalogNotFoundException("Không tìm thấy sản phẩm trong giỏ.");
        db.CartItems.Remove(line);
        await db.SaveChangesAsync(ct);
    }

    public async Task<OrderView> PlaceOrderAsync(CartOwner owner, PlaceOrderInput input, CancellationToken ct = default)
    {
        FashionRules.ValidateOwner(owner);
        FashionRules.ValidateOrder(input);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // EF may invoke this delegate again after a transient SQL failure.
            // Discard state from the failed attempt before reading the cart.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var previous = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.RequestId == input.RequestId, ct);
            if (previous is not null)
            {
                if (!IsOwner(previous, owner)) throw new CatalogValidationException("Yêu cầu đặt hàng không hợp lệ.");
                return View(previous);
            }
            var fee = await db.StoreSettings.Where(s => s.Id == 1).Select(s => s.ShippingFee).SingleOrDefaultAsync(ct);
            if (fee is null) throw new CatalogValidationException("Cửa hàng chưa cấu hình phí vận chuyển.");
            var cart = await CartQuery(owner).Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
                .ThenInclude(p => p.Category).AsSplitQuery().SingleOrDefaultAsync(ct);
            if (cart is null || cart.Items.Count == 0) throw new CatalogValidationException("Giỏ hàng đang trống.");
            if (fee != input.ShippingFee || input.Lines.Count != cart.Items.Count || cart.Items.Any(i =>
                !input.Lines.Any(q => q.VariantId == i.VariantId && q.Quantity == i.Quantity && q.UnitPrice == i.Variant.Product.Price)))
                throw new CatalogValidationException("Giá hoặc giỏ hàng đã thay đổi; vui lòng kiểm tra lại.");
            foreach (var item in cart.Items)
                if (!item.Variant.IsActive || !item.Variant.Product.IsVisible || item.Variant.Product.IsDeleted || !item.Variant.Product.Category.IsVisible || item.Quantity > item.Variant.Stock)
                    throw new CatalogValidationException("Sản phẩm không còn đủ hàng; vui lòng kiểm tra lại giỏ.");
            var order = new FashionOrder
            {
                Id = Guid.NewGuid(), Code = $"XM-{Guid.NewGuid():N}", RequestId = input.RequestId, UserId = owner.UserId,
                GuestKeyHash = owner.GuestKeyHash, RecipientName = input.RecipientName.Trim(), Email = input.Email.Trim(), Phone = input.Phone.Trim(),
                Address = input.Address.Trim(), City = input.City.Trim(), Note = input.Note?.Trim(), ShippingFee = fee.Value,
                Total = cart.Items.Sum(i => i.Variant.Product.Price * i.Quantity) + fee.Value, Status = "Pending"
            };
            foreach (var item in cart.Items.OrderBy(i => i.VariantId))
            {
                var variant = item.Variant;
                variant.Stock -= item.Quantity;
                order.Items.Add(new FashionOrderItem { VariantId = variant.Id, ProductName = variant.Product.Name, Sku = variant.Sku,
                    Color = variant.Color, Size = variant.Size, UnitPrice = variant.Product.Price, Quantity = item.Quantity });
            }
            order.History.Add(new FashionOrderStatusHistory { Status = order.Status, ActorUserId = owner.UserId });
            db.Orders.Add(order);
            db.CartItems.RemoveRange(cart.Items);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return View(order);
        });
    }

    public async Task<OrderView> CancelOrderAsync(CartOwner owner, Guid orderId, CancellationToken ct = default)
    {
        FashionRules.ValidateOwner(owner);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var order = await db.Orders.Include(o => o.Items).ThenInclude(i => i.Variant).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null || !IsOwner(order, owner)) throw new CatalogNotFoundException("Đơn hàng không tồn tại.");
            if (order.Status == "Cancelled") return View(order);
            if (order.Status != "Pending") throw new CatalogValidationException("Chỉ có thể hủy đơn đang chờ xác nhận.");
            foreach (var item in order.Items.OrderBy(i => i.VariantId)) item.Variant.Stock += item.Quantity;
            order.History.Add(new FashionOrderStatusHistory { PreviousStatus = order.Status, Status = "Cancelled", ActorUserId = owner.UserId });
            order.Status = "Cancelled";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return View(order);
        });
    }

    private IQueryable<FashionCart> CartQuery(CartOwner owner) => owner.UserId is int userId
        ? db.Carts.Where(c => c.UserId == userId) : db.Carts.Where(c => c.GuestKeyHash == owner.GuestKeyHash);
    private static bool IsOwner(FashionOrder order, CartOwner owner) => owner.UserId is int userId
        ? order.UserId == userId : order.UserId is null && order.GuestKeyHash == owner.GuestKeyHash;
    private static OrderView View(FashionOrder o) => new(o.Id, o.Code, o.Status, o.ShippingFee, o.Total, o.CreatedAt);
}
