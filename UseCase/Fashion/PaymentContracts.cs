namespace UseCase.Fashion;

public sealed record PaymentMethodView(string Code, string Name, bool Available, string? Message);
public sealed record PaymentRequest(Guid Id, Guid OrderId, string Code, string Provider, decimal Amount, DateTime ExpiresAt);
public sealed record PaymentNotification(string Provider, Guid Reference, decimal Amount, string TransactionId, bool Success, bool Pending = false);
public sealed record PaymentReceipt(string Code, Guid? OrderId = null);
public sealed record PaymentLink(string? Url, string Status, string? Message = null);

public interface IFashionPaymentGateway
{
    IReadOnlyList<PaymentMethodView> Methods();
    Task<string> CreateAsync(PaymentRequest request, string ipAddress, CancellationToken ct);
}

public interface IFashionPayments
{
    Task<PaymentLink> StartAsync(CartOwner owner, Guid orderId, string ipAddress, CancellationToken ct = default);
    Task<PaymentReceipt> NotifyAsync(PaymentNotification notification, CancellationToken ct = default);
    Task<int> ExpireAsync(CancellationToken ct = default);
    Task MarkRefundedAsync(Guid orderId, byte[] version, string reference, int? actor, CancellationToken ct = default);
}

public sealed class PaymentGatewayException(string message) : Exception(message);
