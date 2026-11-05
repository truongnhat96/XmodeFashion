namespace UseCase.Fashion;

public sealed record CatalogQuery(string? Search, int? CategoryId, string? Audience, string? Size,
    string? Color, decimal? MinPrice, decimal? MaxPrice, bool? IsNew, string? Sort, int Page = 1, int PageSize = 12);
public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record CategoryView(int Id, string Name, string? Description, int? ImageId, string? ImageUrl, int SortOrder, bool IsVisible);
public sealed record VariantView(int Id, string Sku, string Color, string Hex, string Size, int Stock, bool IsActive, byte[] Version);
public sealed record ProductImageView(int Id, int MediaAssetId, string Url, string? Alt, int SortOrder, bool IsPrimary);
public sealed record ProductView(int Id, string Code, string Name, string? Brand, int CategoryId, string Audience,
    decimal Price, decimal? OldPrice, string Description, string? Material, string? Fit, string? Care,
    bool IsNew, bool Featured, bool IsVisible, int? SizeChartId, byte[] Version,
    IReadOnlyList<VariantView> Variants, IReadOnlyList<ProductImageView> Images);

public sealed record CategoryInput(string Name, string? Description, int? ImageId, int SortOrder, bool IsVisible);
public sealed record VariantInput(int? Id, string Sku, string Color, string Hex, string Size, int Stock, bool IsActive, byte[]? Version);
public sealed record ProductImageInput(int MediaAssetId, int SortOrder, bool IsPrimary);
public sealed record ProductInput(string Code, string Name, string? Brand, int CategoryId, string Audience,
    decimal Price, decimal? OldPrice, string Description, string? Material, string? Fit, string? Care,
    bool IsNew, bool Featured, bool IsVisible, int? SizeChartId, byte[]? Version,
    IReadOnlyList<VariantInput> Variants, IReadOnlyList<ProductImageInput> Images);

public interface IFashionCatalog
{
    Task<IReadOnlyList<CategoryView>> CategoriesAsync(bool includeHidden, CancellationToken cancellationToken = default);
    Task<CatalogPage<ProductView>> ProductsAsync(CatalogQuery query, bool includeHidden, CancellationToken cancellationToken = default);
    Task<ProductView?> ProductAsync(int id, bool includeHidden, CancellationToken cancellationToken = default);
    Task<CategoryView> SaveCategoryAsync(int? id, CategoryInput input, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductView> SaveProductAsync(int? id, ProductInput input, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(int id, byte[] version, CancellationToken cancellationToken = default);
}

public sealed class CatalogValidationException(string message) : Exception(message);
public sealed class CatalogNotFoundException(string message) : Exception(message);
