using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Data.Fashion;

public sealed class ShoppingChatCatalog(FashionContext db) : IShoppingChatCatalog
{
    private IQueryable<FashionProduct> PublicProducts => db.Products.AsNoTracking()
        .Where(p => !p.IsDeleted && p.IsVisible && !p.Category.IsDeleted && p.Category.IsVisible);

    public async Task<ShoppingCatalogSnapshot> GetSnapshotAsync(string query, int? productId, CancellationToken ct)
    {
        var total = await PublicProducts.CountAsync(ct);
        var stop = new HashSet<string>("toi minh ban cho voi mot nhung nao can muon tim san pham quan ao duoi tren gia mac tu van goi y co khong nay the va la cua shop".Split(' '));
        var terms = Normalize(query).Split([' ', '\n', ',', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2 && t.Length <= 40 && !stop.Contains(t)).Distinct().TakeLast(8).ToArray();
        var ids = new HashSet<int>();
        foreach (var term in terms)
        {
            var matches = await PublicProducts.Where(p =>
                EF.Functions.Like(EF.Functions.Collate(p.Name, "Latin1_General_100_CI_AI"), "%" + term + "%") ||
                EF.Functions.Like(EF.Functions.Collate(p.Category.Name, "Latin1_General_100_CI_AI"), "%" + term + "%") ||
                EF.Functions.Like(EF.Functions.Collate(p.Material ?? "", "Latin1_General_100_CI_AI"), "%" + term + "%"))
                .OrderByDescending(p => p.CreatedAt).Select(p => p.Id).Take(12).ToListAsync(ct);
            ids.UnionWith(matches);
        }
        if (productId is > 0) ids.Add(productId.Value);
        var latest = await PublicProducts.OrderByDescending(p => p.CreatedAt).Select(p => p.Id).Take(20).ToListAsync(ct);
        if (ids.Count < 20) ids.UnionWith(latest);
        var candidates = await GetProductsAsync(ids.ToArray(), ct);
        var ranked = candidates.OrderByDescending(p => p.Id == productId ? 10000 :
            terms.Sum(t => Normalize(p.Name + " " + p.Category + " " + p.Material).Contains(t, StringComparison.Ordinal) ? 1 : 0))
            .ThenBy(p => p.Id).Take(20).ToList();
        var settings = await db.StoreSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        return new(total, ranked, settings?.ShippingFee ?? FashionRules.DefaultShippingFee, settings?.Email, settings?.Phone);
    }

    public async Task<IReadOnlyList<ShoppingProduct>> GetProductsAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var rows = await PublicProducts.Where(p => ids.Contains(p.Id)).Include(p => p.Category)
            .Include(p => p.Variants).Include(p => p.Images).ThenInclude(i => i.MediaAsset)
            .Include(p => p.SizeChart).ThenInclude(s => s!.Rows).AsSplitQuery().ToListAsync(ct);
        return rows.Select(p => new ShoppingProduct(p.Id, p.Name, p.Category.Name, p.Audience, p.Price, p.OldPrice,
            Clip(p.Description, 700)!, Clip(p.Material, 250), Clip(p.Fit, 100), Clip(p.Care, 400),
            p.SizeChart is null ? null : Clip(p.SizeChart.Instructions + "\n" + string.Join("; ", p.SizeChart.Rows.OrderBy(r => r.SortOrder)
                .Select(r => $"{r.Size}: ngực {r.Chest}, eo {r.Waist}, hông {r.Hip} cm")), 800),
            p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault()?.MediaAsset.Url,
            p.Variants.Where(v => v.IsActive).Select(v => new ShoppingVariant(v.Color, v.Size, v.Stock)).ToList())).ToList();
    }
    private static string? Clip(string? value, int length) => value?.Length > length ? value[..length] : value;
    private static string Normalize(string value)
    {
        var text = new StringBuilder();
        foreach (var c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) text.Append(c == 'đ' ? 'd' : c);
        return text.ToString().Normalize(NormalizationForm.FormC);
    }
}
