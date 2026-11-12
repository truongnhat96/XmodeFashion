using System.Security.Claims;
using Infrastructure.Data.Fashion;
using Infrastructure.Fashion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Controllers;

[ApiController, Route("api/xmode"), AutoValidateAntiforgeryToken, ServiceFilter(typeof(FashionErrors))]
public sealed class FashionStoreApiController(IFashionCatalog catalog, IFashionOrders orders, FashionContext db, FashionVisitor visitor, IFashionPaymentGateway gateway) : ControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryView>> Categories(CancellationToken ct) => catalog.CategoriesAsync(false, ct);

    [HttpGet("products")]
    public Task<CatalogPage<ProductView>> Products([FromQuery] string? q, [FromQuery] int? category, [FromQuery] string? audience,
        [FromQuery] string? size, [FromQuery] string? color, [FromQuery] decimal? min, [FromQuery] decimal? max,
        [FromQuery(Name = "new")] int? isNew, [FromQuery] string? sort, [FromQuery] int page = 1, CancellationToken ct = default)
        => catalog.ProductsAsync(new(q, category, audience, size, color, min, max, isNew == 1, sort, page), false, ct);

    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id, CancellationToken ct) => (await catalog.ProductAsync(id, false, ct)) is { } product ? Ok(product) : NotFound();

    [HttpGet("home")]
    public async Task<IActionResult> Home(CancellationToken ct)
    {
        var categories = await catalog.CategoriesAsync(false, ct);
        var newProducts = (await catalog.ProductsAsync(new(null, null, null, null, null, null, null, true, "new", 1, 4), false, ct)).Items;
        var recentProducts = (await catalog.ProductsAsync(new(null, null, null, null, null, null, null, null, "new", 1, 4), false, ct)).Items;
        var ids = await db.Products.Where(p => p.IsVisible && !p.IsDeleted && p.Category.IsVisible && p.Featured).OrderByDescending(p => p.CreatedAt).Select(p => p.Id).Take(4).ToListAsync(ct);
        var featured = new List<ProductView>();
        foreach (var id in ids) if (await catalog.ProductAsync(id, false, ct) is { } product) featured.Add(product);
        var sections = await db.HomeSections.AsNoTracking().Where(s => s.IsVisible).OrderBy(s => s.SortOrder)
            .Select(s => new { s.Id, s.Slot, s.Title, s.Subtitle, s.Body, imageUrl = s.Image == null ? null : s.Image.Url, s.LinkText, s.LinkUrl }).ToListAsync(ct);
        var totalProducts = await db.Products.CountAsync(p => p.IsVisible && !p.IsDeleted && p.Category.IsVisible, ct);
        return Ok(new { categories, newProducts, recentProducts, featured, sections, totalProducts });
    }

    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken ct)
    {
        var variants = db.ProductVariants.Where(v => v.IsActive && v.Stock > 0 && v.Product.IsVisible && !v.Product.IsDeleted && v.Product.Category.IsVisible);
        return Ok(new { sizes = await variants.Select(v => v.Size).Distinct().OrderBy(x => x).ToListAsync(ct), colors = await variants.Select(v => v.Color).Distinct().OrderBy(x => x).ToListAsync(ct) });
    }

    [HttpGet("size-charts")]
    public async Task<IActionResult> SizeCharts(CancellationToken ct) => Ok(await db.SizeCharts.AsNoTracking().Include(x => x.Rows).AsSplitQuery().ToListAsync(ct));
    [HttpGet("size-charts/{id:int}")]
    public async Task<IActionResult> SizeChart(int id, CancellationToken ct) => (await db.SizeCharts.AsNoTracking().Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == id, ct)) is { } chart ? Ok(chart) : NotFound();
    [HttpGet("store-settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var settings = await db.StoreSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct) ?? new StoreSettings { Id = 1 };
        settings.ShippingFee ??= FashionRules.DefaultShippingFee;
        return Ok(settings);
    }

    [HttpGet("cart")]
    public Task<CartView> Cart(CancellationToken ct) => orders.GetCartAsync(visitor.Owner(HttpContext), ct);

    public record CartInput(int VariantId, int Quantity);
    [HttpPost("cart/items")]
    public async Task<IActionResult> AddCart(CartInput input, CancellationToken ct)
    {
        if (input.Quantity <= 0) throw new CatalogValidationException("Số lượng phải lớn hơn 0.");
        var owner = visitor.Owner(HttpContext);
        var current = await orders.GetCartAsync(owner, ct);
        var quantity = checked(input.Quantity + (current.Items.FirstOrDefault(x => x.VariantId == input.VariantId)?.Quantity ?? 0));
        await orders.SetCartItemAsync(owner, input.VariantId, quantity, ct);
        return Ok(await orders.GetCartAsync(owner, ct));
    }
    public record QuantityInput(int Quantity);
    [HttpPatch("cart/items/{id:guid}")]
    public async Task<IActionResult> ChangeCart(Guid id, QuantityInput input, CancellationToken ct)
    {
        var owner = visitor.Owner(HttpContext);
        var cart = await orders.GetCartAsync(owner, ct);
        var item = cart.Items.SingleOrDefault(x => x.Id == id) ?? throw new CatalogNotFoundException("Không tìm thấy sản phẩm trong giỏ.");
        await orders.SetCartItemAsync(owner, item.VariantId, input.Quantity, ct);
        return Ok(await orders.GetCartAsync(owner, ct));
    }
    [HttpDelete("cart/items/{id:guid}")]
    public async Task<IActionResult> RemoveCart(Guid id, CancellationToken ct) { await orders.RemoveCartItemAsync(visitor.Owner(HttpContext), id, ct); return NoContent(); }

    [HttpPost("orders")]
    public Task<OrderView> PlaceOrder(PlaceOrderInput input, CancellationToken ct)
    {
        if (!gateway.Methods().Any(m => m.Code == input.PaymentMethod && m.Available))
            throw new CatalogValidationException("Phương thức thanh toán chưa được cấu hình hoặc đang tạm ngưng.");
        return orders.PlaceOrderAsync(visitor.Owner(HttpContext), input, ct);
    }
    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<OrderView> Cancel(Guid id, CancellationToken ct)
    {
        var owner = visitor.Owner(HttpContext);
        // A signed-in customer may also cancel an order created earlier in this guest session.
        if (owner.UserId is not null && !await db.Orders.AnyAsync(x => x.Id == id && x.UserId == owner.UserId, ct)) owner = new(null, visitor.GuestHash(HttpContext));
        return await orders.CancelOrderAsync(owner, id, ct);
    }
    [HttpGet("orders")]
    public async Task<IActionResult> MyOrders(CancellationToken ct)
    {
        var guest = visitor.GuestHash(HttpContext);
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
        var list = await db.Orders.AsNoTracking().Where(o => (userId != null && o.UserId == userId) || (o.UserId == null && o.GuestKeyHash == guest))
            .OrderByDescending(o => o.CreatedAt).Select(o => new { o.Id, o.Code, o.Status, o.Total, o.ShippingFee, o.CreatedAt, o.PaymentMethod, o.PaymentStatus }).ToListAsync(ct);
        return Ok(list);
    }
    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> MyOrder(Guid id, CancellationToken ct)
    {
        var guest = visitor.GuestHash(HttpContext);
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.History).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == id &&
            ((userId != null && o.UserId == userId) || (o.UserId == null && o.GuestKeyHash == guest)), ct);
        return order is null ? NotFound() : Ok(OrderResponse(order));
    }
    internal static object OrderResponse(FashionOrder o) => new { o.Id, o.Code, o.Status, o.RecipientName, o.Email, o.Phone, o.Address, o.City, o.Note,
        o.ShippingFee, o.Total, o.CreatedAt, o.PaymentMethod, o.PaymentStatus, o.PaymentExpiresAt, o.PaidAt, o.IsArchived, o.Version, items = o.Items.Select(i => new { i.ProductName, i.Sku, i.Color, i.Size, i.UnitPrice, i.Quantity }),
        history = o.History.OrderBy(h => h.CreatedAt).Select(h => new { h.PreviousStatus, h.Status, h.Reason, h.CreatedAt }) };
}
