namespace UseCase.Fashion;

public sealed record ShoppingChatTurn(string Role, string Content);
public sealed record ShoppingVariant(string Color, string Size, int Stock);
public sealed record ShoppingProduct(int Id, string Name, string Category, string Audience, decimal Price,
    decimal? OldPrice, string Description, string? Material, string? Fit, string? Care,
    string? SizeGuide, string? ImageUrl, IReadOnlyList<ShoppingVariant> Variants);
public sealed record ShoppingCatalogSnapshot(int TotalProducts, IReadOnlyList<ShoppingProduct> Products,
    decimal? ShippingFee, string? ContactEmail, string? ContactPhone);
public sealed record ShoppingModelReply(string Message, IReadOnlyList<int> ProductIds);
public sealed record ShoppingProductCard(int Id, string Name, decimal Price, decimal? OldPrice, string? ImageUrl,
    string Url, IReadOnlyList<ShoppingVariant> Variants);
public sealed record ShoppingChatReply(string Message, IReadOnlyList<ShoppingProductCard> Products);

public interface IShoppingChatCatalog
{
    Task<ShoppingCatalogSnapshot> GetSnapshotAsync(string query, int? productId, CancellationToken ct);
    Task<IReadOnlyList<ShoppingProduct>> GetProductsAsync(IReadOnlyList<int> ids, CancellationToken ct);
}
public interface IShoppingLanguageModel
{
    bool IsConfigured { get; }
    Task<ShoppingModelReply> ReplyAsync(ShoppingCatalogSnapshot catalog, IReadOnlyList<ShoppingChatTurn> history, string message, CancellationToken ct);
}
public interface IShoppingAssistant
{
    Task<ShoppingChatReply> ReplyAsync(string message, int? productId, IReadOnlyList<ShoppingChatTurn> history, CancellationToken ct);
}
public sealed class ShoppingChatUnavailableException(string message, int statusCode = 503) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ShoppingAssistant(IShoppingChatCatalog catalog, IShoppingLanguageModel model) : IShoppingAssistant
{
    public async Task<ShoppingChatReply> ReplyAsync(string message, int? productId, IReadOnlyList<ShoppingChatTurn> history, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 1200)
            throw new CatalogValidationException("Vui lòng nhập câu hỏi từ 1 đến 1.200 ký tự.");
        var query = string.Join(" ", history.Where(t => t.Role == "user").TakeLast(2).Select(t => t.Content).Append(message));
        var snapshot = await catalog.GetSnapshotAsync(query, productId, ct);
        if (snapshot.TotalProducts == 0)
            return new("Cửa hàng hiện chưa có sản phẩm công khai. Bạn có thể quay lại sau để chọn trang phục phù hợp.", []);
        var answer = await model.ReplyAsync(snapshot, history, message.Trim(), ct);
        if (string.IsNullOrWhiteSpace(answer.Message) || answer.Message.Length > 3000 || answer.ProductIds.Count > 4 ||
            answer.ProductIds.Any(id => !snapshot.Products.Any(p => p.Id == id)))
            throw new ShoppingChatUnavailableException("Chưa thể xác nhận gợi ý phù hợp. Bạn vui lòng thử hỏi cụ thể hơn.", 502);
        // Reload after generation so hidden/deleted products and stale prices never become product cards.
        var current = await catalog.GetProductsAsync(answer.ProductIds.Distinct().ToArray(), ct);
        var cards = answer.ProductIds.Distinct().Select(id => current.FirstOrDefault(p => p.Id == id))
            .Where(p => p != null).Select(p => new ShoppingProductCard(p!.Id, p.Name, p.Price, p.OldPrice,
                p.ImageUrl, $"/xmode/product/{p.Id}", p.Variants)).ToList();
        var changed = cards.Count != answer.ProductIds.Distinct().Count();
        return new(answer.Message.Trim() + (changed ? "\nMột số sản phẩm vừa thay đổi; danh sách bên dưới đã được cập nhật." : ""), cards);
    }
}
