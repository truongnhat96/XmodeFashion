using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Data.Fashion;

public sealed class FashionCatalog(FashionContext db) : IFashionCatalog
{
    public async Task<IReadOnlyList<CategoryView>> CategoriesAsync(bool includeHidden, CancellationToken ct = default)
    {
        var query = db.Categories.AsNoTracking().Include(x => x.Image).Where(x => !x.IsDeleted);
        if (!includeHidden) query = query.Where(x => x.IsVisible);
        return (await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct))
            .Select(Category).ToList();
    }

    public async Task<CatalogPage<ProductView>> ProductsAsync(CatalogQuery q, bool includeHidden, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Where(p => !p.IsDeleted);
        if (!includeHidden) query = query.Where(p => p.IsVisible && p.Category.IsVisible);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            query = query.Where(p => EF.Functions.Like(EF.Functions.Collate(p.Name, "Latin1_General_100_CI_AI"), "%" + term + "%") ||
                                     EF.Functions.Like(EF.Functions.Collate(p.Material ?? "", "Latin1_General_100_CI_AI"), "%" + term + "%"));
        }
        if (q.CategoryId is not null) query = query.Where(p => p.CategoryId == q.CategoryId);
        if (!string.IsNullOrWhiteSpace(q.Audience)) query = query.Where(p => p.Audience == q.Audience);
        if (q.MinPrice is not null) query = query.Where(p => p.Price >= q.MinPrice);
        if (q.MaxPrice is not null) query = query.Where(p => p.Price <= q.MaxPrice);
        if (q.IsNew == true) query = query.Where(p => p.IsNew);
        if (!string.IsNullOrWhiteSpace(q.Size) || !string.IsNullOrWhiteSpace(q.Color))
            query = query.Where(p => p.Variants.Any(v => v.IsActive && v.Stock > 0 &&
                (string.IsNullOrWhiteSpace(q.Size) || v.Size == q.Size) &&
                (string.IsNullOrWhiteSpace(q.Color) || v.Color == q.Color)));
        var total = await query.CountAsync(ct);
        query = q.Sort switch
        {
            "asc" => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        var page = Math.Clamp(q.Page, 1, Math.Max(1, (int)Math.Ceiling((double)total / pageSize)));
        var products = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Include(p => p.Variants).Include(p => p.Images).ThenInclude(i => i.MediaAsset)
            .AsSplitQuery().ToListAsync(ct);
        return new(products.Select(Product).ToList(), total, page, pageSize);
    }

    public async Task<ProductView?> ProductAsync(int id, bool includeHidden, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Where(p => p.Id == id && !p.IsDeleted);
        if (!includeHidden) query = query.Where(p => p.IsVisible && p.Category.IsVisible);
        var product = await query.Include(p => p.Variants).Include(p => p.Images).ThenInclude(i => i.MediaAsset)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
        return product is null ? null : Product(product);
    }

    public async Task<CategoryView> SaveCategoryAsync(int? id, CategoryInput input, CancellationToken ct = default)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100 || input.Description?.Length > 1000) throw new CatalogValidationException("Tên hoặc mô tả danh mục không hợp lệ.");
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != id && !c.IsDeleted, ct)) throw new CatalogValidationException("Tên danh mục đã tồn tại.");
        if (input.ImageId is not null && !await db.MediaAssets.AnyAsync(i => i.Id == input.ImageId, ct)) throw new CatalogValidationException("Ảnh không tồn tại.");
        var category = id is null ? new FashionCategory { Name = name } : await db.Categories.SingleOrDefaultAsync(c => c.Id == id.Value && !c.IsDeleted, ct)
            ?? throw new CatalogNotFoundException("Danh mục không tồn tại.");
        if (id is null) db.Categories.Add(category);
        category.Name = name;
        category.Description = input.Description?.Trim();
        category.ImageId = input.ImageId;
        category.SortOrder = input.SortOrder;
        category.IsVisible = input.IsVisible;
        await db.SaveChangesAsync(ct);
        if (category.ImageId is not null) await db.Entry(category).Reference(x => x.Image).LoadAsync(ct);
        return Category(category);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct) ?? throw new CatalogNotFoundException("Danh mục không tồn tại.");
        if (await db.Products.AnyAsync(p => p.CategoryId == id && !p.IsDeleted, ct)) throw new CatalogValidationException("Danh mục còn sản phẩm; hãy chuyển sản phẩm trước.");
        category.IsDeleted = true;
        category.IsVisible = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProductView> SaveProductAsync(int? id, ProductInput input, CancellationToken ct = default)
    {
        FashionRules.ValidateProduct(input);
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId && !c.IsDeleted, ct)) throw new CatalogValidationException("Danh mục không tồn tại.");
        if (input.SizeChartId is not null && !await db.SizeCharts.AnyAsync(c => c.Id == input.SizeChartId, ct)) throw new CatalogValidationException("Bảng size không tồn tại.");
        var imageIds = input.Images.Select(i => i.MediaAssetId).Distinct().ToArray();
        if (await db.MediaAssets.CountAsync(i => imageIds.Contains(i.Id), ct) != imageIds.Length) throw new CatalogValidationException("Ảnh không tồn tại.");
        var product = id is null ? new FashionProduct { Code = input.Code.Trim(), Name = input.Name.Trim(), Audience = input.Audience, Description = input.Description.Trim() } :
            await db.Products.Include(p => p.Variants).Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
                ?? throw new CatalogNotFoundException("Sản phẩm không tồn tại.");
        if (id is null) db.Products.Add(product);
        else
        {
            if (input.Version is null || !product.Version.SequenceEqual(input.Version)) throw new DbUpdateConcurrencyException("Sản phẩm đã thay đổi.");
            db.Entry(product).Property(p => p.Version).OriginalValue = input.Version;
        }
        product.Code = input.Code.Trim(); product.Name = input.Name.Trim(); product.Brand = input.Brand?.Trim();
        product.CategoryId = input.CategoryId; product.Audience = input.Audience; product.Price = input.Price; product.OldPrice = input.OldPrice;
        product.Description = input.Description.Trim(); product.Material = input.Material?.Trim(); product.Fit = input.Fit?.Trim(); product.Care = input.Care?.Trim();
        product.IsNew = input.IsNew; product.Featured = input.Featured; product.IsVisible = input.IsVisible; product.SizeChartId = input.SizeChartId; product.UpdatedAt = DateTime.UtcNow;
        var suppliedIds = input.Variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        foreach (var existing in product.Variants.Where(v => !suppliedIds.Contains(v.Id))) existing.IsActive = false;
        foreach (var v in input.Variants)
        {
            var variant = v.Id is null ? new ProductVariant { Sku = v.Sku.Trim(), Color = v.Color.Trim(), Hex = v.Hex, Size = v.Size.Trim() } :
                product.Variants.SingleOrDefault(x => x.Id == v.Id) ?? throw new CatalogValidationException("Biến thể không thuộc sản phẩm.");
            if (v.Id is null) product.Variants.Add(variant);
            else
            {
                if (v.Version is null || !variant.Version.SequenceEqual(v.Version)) throw new DbUpdateConcurrencyException("Biến thể đã thay đổi.");
                db.Entry(variant).Property(x => x.Version).OriginalValue = v.Version;
            }
            variant.Sku = v.Sku.Trim(); variant.Color = v.Color.Trim(); variant.Hex = v.Hex; variant.Size = v.Size.Trim().ToUpperInvariant();
            variant.Stock = v.Stock; variant.IsActive = v.IsActive;
        }
        db.ProductImages.RemoveRange(product.Images);
        product.Images = input.Images.Select(i => new FashionProductImage { MediaAssetId = i.MediaAssetId, SortOrder = i.SortOrder, IsPrimary = i.IsPrimary }).ToList();
        await db.SaveChangesAsync(ct);
        return (await ProductAsync(product.Id, true, ct))!;
    }

    public async Task DeleteProductAsync(int id, byte[] version, CancellationToken ct = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct) ?? throw new CatalogNotFoundException("Sản phẩm không tồn tại.");
        if (!product.Version.SequenceEqual(version)) throw new DbUpdateConcurrencyException("Sản phẩm đã thay đổi.");
        db.Entry(product).Property(x => x.Version).OriginalValue = version;
        product.IsDeleted = true; product.IsVisible = false; product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static CategoryView Category(FashionCategory c) => new(c.Id, c.Name, c.Description, c.ImageId, c.Image?.Url, c.SortOrder, c.IsVisible);
    private static ProductView Product(FashionProduct p) => new(p.Id, p.Code, p.Name, p.Brand, p.CategoryId, p.Audience, p.Price, p.OldPrice,
        p.Description, p.Material, p.Fit, p.Care, p.IsNew, p.Featured, p.IsVisible, p.SizeChartId, p.Version,
        p.Variants.Select(v => new VariantView(v.Id, v.Sku, v.Color, v.Hex, v.Size, v.Stock, v.IsActive, v.Version)).ToList(),
        p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => new ProductImageView(i.Id, i.MediaAssetId, i.MediaAsset.Url, i.MediaAsset.Alt, i.SortOrder, i.IsPrimary)).ToList());
}
