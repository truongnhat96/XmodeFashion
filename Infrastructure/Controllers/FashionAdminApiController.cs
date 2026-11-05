using System.Security.Claims;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Infrastructure.Data.Fashion;
using Infrastructure.Fashion;
using Infrastructure.Models.Cloudinary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UseCase.Fashion;

namespace Infrastructure.Controllers;

[ApiController, Route("api/xmode/admin"), Authorize(Roles = "Admin"), AutoValidateAntiforgeryToken, ServiceFilter(typeof(FashionErrors))]
public sealed class FashionAdminApiController(IFashionCatalog catalog, FashionContext db, IOptions<CloudinarySettings> cloudOptions) : ControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryView>> Categories(CancellationToken ct) => catalog.CategoriesAsync(true, ct);
    [HttpPost("categories")]
    public Task<CategoryView> CreateCategory(CategoryInput input, CancellationToken ct) => catalog.SaveCategoryAsync(null, input, ct);
    [HttpPut("categories/{id:int}")]
    public Task<CategoryView> UpdateCategory(int id, CategoryInput input, CancellationToken ct) => catalog.SaveCategoryAsync(id, input, ct);
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct) { await catalog.DeleteCategoryAsync(id, ct); return NoContent(); }

    [HttpGet("products")]
    public Task<CatalogPage<ProductView>> Products([FromQuery] string? q, [FromQuery] int page = 1, CancellationToken ct = default)
        => catalog.ProductsAsync(new(q, null, null, null, null, null, null, null, null, page, 20), true, ct);
    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id, CancellationToken ct) => (await catalog.ProductAsync(id, true, ct)) is { } p ? Ok(p) : NotFound();
    [HttpPost("products")]
    public Task<ProductView> CreateProduct(ProductInput input, CancellationToken ct) => catalog.SaveProductAsync(null, input, ct);
    [HttpPut("products/{id:int}")]
    public Task<ProductView> UpdateProduct(int id, ProductInput input, CancellationToken ct) => catalog.SaveProductAsync(id, input, ct);
    public record DeleteProductInput(byte[] Version);
    [HttpPost("products/{id:int}/delete")]
    public async Task<IActionResult> DeleteProduct(int id, DeleteProductInput input, CancellationToken ct) { await catalog.DeleteProductAsync(id, input.Version, ct); return NoContent(); }

    [HttpGet("home-sections")]
    public async Task<IActionResult> Sections(CancellationToken ct) => Ok(await db.HomeSections.AsNoTracking().Include(s => s.Image).OrderBy(s => s.SortOrder)
        .Select(s => new { s.Id, s.Slot, s.Title, s.Subtitle, s.Body, s.ImageId, imageUrl = s.Image == null ? null : s.Image.Url, s.LinkText, s.LinkUrl, s.SortOrder, s.IsVisible }).ToListAsync(ct));
    public record SectionInput(string Slot, string Title, string? Subtitle, string? Body, int? ImageId, string? LinkText, string? LinkUrl, int SortOrder, bool IsVisible);
    [HttpPost("home-sections")]
    public Task<object> CreateSection(SectionInput input, CancellationToken ct) => SaveSection(null, input, ct);
    [HttpPut("home-sections/{id:int}")]
    public Task<object> UpdateSection(int id, SectionInput input, CancellationToken ct) => SaveSection(id, input, ct);
    private async Task<object> SaveSection(int? id, SectionInput x, CancellationToken ct)
    {
        if (x.Slot is not ("hero" or "editorial") || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 200 || x.Subtitle?.Length > 200 || x.Body?.Length > 2000 || x.LinkText?.Length > 100 || x.LinkUrl?.Length > 500 ||
            (!string.IsNullOrEmpty(x.LinkUrl) && (!x.LinkUrl.StartsWith('/') || x.LinkUrl.StartsWith("//")))) throw new CatalogValidationException("Nội dung hoặc đường dẫn không hợp lệ.");
        if (x.ImageId != null && !await db.MediaAssets.AnyAsync(m => m.Id == x.ImageId, ct)) throw new CatalogValidationException("Ảnh không tồn tại.");
        var section = id is null ? new HomeSection { Slot = x.Slot, Title = x.Title } : await db.HomeSections.FindAsync([id.Value], ct) ?? throw new CatalogNotFoundException("Nội dung không tồn tại.");
        if (id is null) db.HomeSections.Add(section);
        section.Slot = x.Slot; section.Title = x.Title.Trim(); section.Subtitle = x.Subtitle; section.Body = x.Body; section.ImageId = x.ImageId;
        section.LinkText = x.LinkText; section.LinkUrl = x.LinkUrl; section.SortOrder = x.SortOrder; section.IsVisible = x.IsVisible;
        await db.SaveChangesAsync(ct); return new { section.Id };
    }
    [HttpDelete("home-sections/{id:int}")]
    public async Task<IActionResult> DeleteSection(int id, CancellationToken ct)
    { var x = await db.HomeSections.FindAsync([id], ct) ?? throw new CatalogNotFoundException("Nội dung không tồn tại."); db.HomeSections.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }

    public record ChartRowInput(string Size, string? Chest, string? Waist, string? Hip, int SortOrder);
    public record ChartInput(string Name, string? Instructions, List<ChartRowInput> Rows);
    [HttpGet("size-charts")]
    public async Task<IActionResult> Charts(CancellationToken ct) => Ok(await db.SizeCharts.AsNoTracking().Include(c => c.Rows).AsSplitQuery().ToListAsync(ct));
    [HttpPost("size-charts")]
    public Task<object> CreateChart(ChartInput input, CancellationToken ct) => SaveChart(null, input, ct);
    [HttpPut("size-charts/{id:int}")]
    public Task<object> UpdateChart(int id, ChartInput input, CancellationToken ct) => SaveChart(id, input, ct);
    private async Task<object> SaveChart(int? id, ChartInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100 || input.Instructions?.Length > 2000 || input.Rows is null || input.Rows.Count == 0 ||
            input.Rows.Any(r => string.IsNullOrWhiteSpace(r.Size) || r.Size.Length > 20 || r.Chest?.Length > 50 || r.Waist?.Length > 50 || r.Hip?.Length > 50) ||
            input.Rows.Select(r => r.Size.Trim().ToUpperInvariant()).Distinct().Count() != input.Rows.Count) throw new CatalogValidationException("Bảng size không hợp lệ.");
        var chart = id is null ? new SizeChart { Name = input.Name } : await db.SizeCharts.Include(c => c.Rows).SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new CatalogNotFoundException("Bảng size không tồn tại.");
        if (id is null) db.SizeCharts.Add(chart);
        chart.Name = input.Name.Trim(); chart.Instructions = input.Instructions;
        var keys = input.Rows.Select(r => r.Size.Trim().ToUpperInvariant()).ToHashSet();
        foreach (var row in chart.Rows.Where(r => !keys.Contains(r.Size.ToUpperInvariant())).ToList()) db.SizeChartRows.Remove(row);
        foreach (var row in input.Rows)
        {
            var size = row.Size.Trim().ToUpperInvariant();
            var existing = chart.Rows.FirstOrDefault(r => r.Size.ToUpperInvariant() == size);
            if (existing is null) { existing = new SizeChartRow { Size = size }; chart.Rows.Add(existing); }
            existing.Chest = row.Chest; existing.Waist = row.Waist; existing.Hip = row.Hip; existing.SortOrder = row.SortOrder;
        }
        await db.SaveChangesAsync(ct); return new { chart.Id };
    }
    [HttpDelete("size-charts/{id:int}")]
    public async Task<IActionResult> DeleteChart(int id, CancellationToken ct)
    {
        var chart = await db.SizeCharts.FindAsync([id], ct) ?? throw new CatalogNotFoundException("Bảng size không tồn tại.");
        if (await db.Products.AnyAsync(p => p.SizeChartId == id, ct)) throw new CatalogValidationException("Bảng size đang được sản phẩm sử dụng.");
        db.SizeCharts.Remove(chart); await db.SaveChangesAsync(ct); return NoContent();
    }

    public record SettingsInput(string? Name, string? Tagline, string? Email, string? Phone, decimal? ShippingFee);
    [HttpGet("store-settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await db.StoreSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct) ?? new StoreSettings { Id = 1 });
    [HttpPut("store-settings")]
    public async Task<IActionResult> Settings(SettingsInput x, CancellationToken ct)
    {
        if (x.Name?.Length > 100 || x.Tagline?.Length > 250 || x.Email?.Length > 100 || x.Phone?.Length > 25 || x.ShippingFee < 0) throw new CatalogValidationException("Thiết lập không hợp lệ.");
        var settings = await db.StoreSettings.FindAsync([1], ct);
        if (settings is null) { settings = new StoreSettings { Id = 1 }; db.StoreSettings.Add(settings); }
        settings.Name = x.Name; settings.Tagline = x.Tagline; settings.Email = x.Email; settings.Phone = x.Phone; settings.ShippingFee = x.ShippingFee;
        await db.SaveChangesAsync(ct); return Ok(settings);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] string? status, [FromQuery] string? q, [FromQuery] bool archived = false, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking().Where(o => o.IsArchived == archived);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(o => o.Code.Contains(q) || o.Phone.Contains(q) || o.RecipientName.Contains(q));
        const int pageSize = 20;
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)total / pageSize)));
        var items = await query.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new { o.Id, o.Code, o.RecipientName, o.Phone, o.Status, o.Total, o.CreatedAt, o.IsArchived, o.Version }).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }
    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct)
    { var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.History).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == id, ct); return order is null ? NotFound() : Ok(FashionStoreApiController.OrderResponse(order)); }
    public record OrderUpdateInput(byte[] Version, string RecipientName, string Email, string Phone, string Address, string City, string? Note);
    [HttpPut("orders/{id:guid}")]
    public async Task<IActionResult> UpdateOrder(Guid id, OrderUpdateInput x, CancellationToken ct)
    {
        if (x.Version is null || string.IsNullOrWhiteSpace(x.RecipientName) || x.RecipientName.Length > 100 || string.IsNullOrWhiteSpace(x.Email) || x.Email.Length > 100 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(x.Email) || string.IsNullOrWhiteSpace(x.Phone) || x.Phone.Length > 25 ||
            string.IsNullOrWhiteSpace(x.Address) || x.Address.Length > 300 || string.IsNullOrWhiteSpace(x.City) || x.City.Length > 100 || x.Note?.Length > 1000) throw new CatalogValidationException("Thông tin người nhận không hợp lệ.");
        var o = await db.Orders.FindAsync([id], ct) ?? throw new CatalogNotFoundException("Đơn hàng không tồn tại.");
        if (o.Status != "Pending") throw new CatalogValidationException("Chỉ sửa đơn đang chờ xác nhận.");
        db.Entry(o).Property(z => z.Version).OriginalValue = x.Version;
        o.RecipientName = x.RecipientName.Trim(); o.Email = x.Email.Trim(); o.Phone = x.Phone.Trim(); o.Address = x.Address.Trim(); o.City = x.City.Trim(); o.Note = x.Note?.Trim();
        o.History.Add(new FashionOrderStatusHistory { PreviousStatus = o.Status, Status = o.Status, ActorUserId = ActorId(), Reason = "Cập nhật thông tin nhận hàng" });
        await db.SaveChangesAsync(ct); return Ok(new { o.Version });
    }
    public record StatusInput(byte[] Version, string Status, string? Reason, bool GoodsReturned);
    [HttpPost("orders/{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, StatusInput input, CancellationToken ct)
    {
        if (input.Version is null || input.Reason?.Length > 1000) throw new CatalogValidationException("Thông tin không hợp lệ.");
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var order = await db.Orders.Include(o => o.Items).ThenInclude(i => i.Variant).Include(o => o.History).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == id, ct) ?? throw new CatalogNotFoundException("Đơn hàng không tồn tại.");
            if (!order.Version.SequenceEqual(input.Version)) throw new DbUpdateConcurrencyException();
            var allowed = FashionRules.CanTransition(order.Status, input.Status, input.GoodsReturned);
            if (!allowed) throw new CatalogValidationException("Không thể chuyển trạng thái này; khi hủy đơn đang giao cần xác nhận đã nhận lại hàng.");
            if (input.Status == "Cancelled") foreach (var item in order.Items.OrderBy(i => i.VariantId)) item.Variant.Stock += item.Quantity;
            order.History.Add(new FashionOrderStatusHistory { PreviousStatus = order.Status, Status = input.Status, ActorUserId = ActorId(), Reason = input.Reason });
            order.Status = input.Status;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
        return NoContent();
    }
    [HttpPost("orders/{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, DeleteProductInput input, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([id], ct) ?? throw new CatalogNotFoundException("Đơn hàng không tồn tại.");
        if (order.Status is not ("Completed" or "Cancelled")) throw new CatalogValidationException("Chỉ lưu trữ đơn đã hoàn tất hoặc đã hủy.");
        db.Entry(order).Property(o => o.Version).OriginalValue = input.Version; order.IsArchived = true;
        await db.SaveChangesAsync(ct); return NoContent();
    }
    private int? ActorId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("media")]
    public async Task<IActionResult> Media(CancellationToken ct) => Ok(await db.MediaAssets.AsNoTracking().OrderByDescending(m => m.CreatedAt).Select(m => new { m.Id, m.Url, m.Alt }).ToListAsync(ct));
    [HttpPost("media"), RequestSizeLimit(5_500_000)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length is <= 0 or > 5_000_000) throw new CatalogValidationException("Ảnh phải nhỏ hơn 5 MB.");
        await using var stream = file.OpenReadStream();
        var header = new byte[12]; var read = await stream.ReadAsync(header, ct); stream.Position = 0;
        var valid = (read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff) ||
            (read >= 8 && header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) ||
            (read >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP");
        if (!valid) throw new CatalogValidationException("Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");
        var cfg = cloudOptions.Value;
        if (string.IsNullOrWhiteSpace(cfg.CloudName) || string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.ApiSecret) || cfg.CloudName == "your_cloud_name") throw new CatalogValidationException("Cloudinary chưa được cấu hình.");
        var cloud = new Cloudinary(new Account(cfg.CloudName, cfg.ApiKey, cfg.ApiSecret));
        var uploaded = await cloud.UploadAsync(new ImageUploadParams { File = new FileDescription(file.FileName, stream), Folder = "xmode-fashion" });
        if (uploaded.Error is not null || uploaded.SecureUrl is null || string.IsNullOrEmpty(uploaded.PublicId)) throw new CatalogValidationException("Không thể tải ảnh lên Cloudinary.");
        var asset = new MediaAsset { Url = uploaded.SecureUrl.ToString(), PublicId = uploaded.PublicId, Alt = Path.GetFileNameWithoutExtension(file.FileName)[..Math.Min(250, Path.GetFileNameWithoutExtension(file.FileName).Length)] };
        db.MediaAssets.Add(asset);
        try { await db.SaveChangesAsync(ct); }
        catch { await cloud.DestroyAsync(new DeletionParams(uploaded.PublicId)); throw; }
        return Ok(new { asset.Id, asset.Url, asset.Alt });
    }
    [HttpDelete("media/{id:int}")]
    public async Task<IActionResult> DeleteMedia(int id, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            // Hold an update lock until deletion commits, so a concurrent FK insert
            // cannot attach this image between the reference check and deletion.
            var asset = await db.MediaAssets.FromSqlInterpolated($"SELECT * FROM [MediaAssets] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct)
                ?? throw new CatalogNotFoundException("Ảnh không tồn tại.");
            if (await db.ProductImages.AnyAsync(x => x.MediaAssetId == id, ct) || await db.Categories.AnyAsync(x => x.ImageId == id, ct) || await db.HomeSections.AnyAsync(x => x.ImageId == id, ct) || await db.Posts.AnyAsync(x => x.ImageUrl == asset.Url, ct)) throw new CatalogValidationException("Ảnh đang được sử dụng.");
            var cfg = cloudOptions.Value; var cloud = new Cloudinary(new Account(cfg.CloudName, cfg.ApiKey, cfg.ApiSecret));
            var result = await cloud.DestroyAsync(new DeletionParams(asset.PublicId));
            if (result.Result is not ("ok" or "not found")) throw new CatalogValidationException("Không thể xóa ảnh trên Cloudinary.");
            db.MediaAssets.Remove(asset); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
        return NoContent();
    }
}
