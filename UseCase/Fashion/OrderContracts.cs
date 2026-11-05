namespace UseCase.Fashion;

// Owner is derived from the authenticated principal or a protected guest cookie,
// never directly from a request body.
public sealed record CartOwner(int? UserId, string? GuestKeyHash);
public sealed record CartLineView(Guid Id, int VariantId, int ProductId, string ProductName, string? ImageUrl, string Sku, string Color, string Size,
    decimal Price, int Quantity, int AvailableStock, bool Available);
public sealed record CartView(IReadOnlyList<CartLineView> Items, decimal Subtotal, decimal? ShippingFee);
public sealed record OrderLineQuote(int VariantId, int Quantity, decimal UnitPrice);
public sealed record PlaceOrderInput(Guid RequestId, string RecipientName, string Email, string Phone, string Address,
    string City, string? Note, decimal ShippingFee, IReadOnlyList<OrderLineQuote> Lines);
public sealed record OrderView(Guid Id, string Code, string Status, decimal ShippingFee, decimal Total, DateTime CreatedAt);

public interface IFashionOrders
{
    Task<CartView> GetCartAsync(CartOwner owner, CancellationToken cancellationToken = default);
    Task SetCartItemAsync(CartOwner owner, int variantId, int quantity, CancellationToken cancellationToken = default);
    Task RemoveCartItemAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken = default);
    Task<OrderView> PlaceOrderAsync(CartOwner owner, PlaceOrderInput input, CancellationToken cancellationToken = default);
    Task<OrderView> CancelOrderAsync(CartOwner owner, Guid orderId, CancellationToken cancellationToken = default);
}
