using Infrastructure.Data.Fashion;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

// Only this GUID-named scratch database is created and deleted; application
// connection strings are not read and the main application is never started.
var database = $"XmodeFashion_Check_{Guid.NewGuid():N}";
var master = new SqlConnectionStringBuilder { DataSource = @".\SQLEXPRESS", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = database };
var created = false;
try
{
    await using (var connection = new SqlConnection(master.ConnectionString))
    {
        await connection.OpenAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{database}]";
        await create.ExecuteNonQueryAsync();
        created = true;
    }

    var options = new DbContextOptionsBuilder<FashionContext>().UseSqlServer(test.ConnectionString,
        sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(1), null)).Options;
    await using var db = new FashionContext(options);
    await db.Database.MigrateAsync();
    await db.Database.MigrateAsync();
    Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 3, "fashion migrations are independent and repeatable");
    Check(!await db.Products.AnyAsync() && !await db.Categories.AnyAsync(), "catalog starts empty");
    var catalog = new FashionCatalog(db);
    var category = await catalog.SaveCategoryAsync(null, new("Áo thun", null, null, 0, true));
    var product = await catalog.SaveProductAsync(null, new("AO-01", "Áo cà phê", null, category.Id, "Unisex", 200000, null,
        "Áo mặc hàng ngày", "Cotton", "Regular", null, true, false, true, null, null,
        [new(null, "AO-01-DEN-M", "Đen", "#222222", "M", 3, true, null),
         new(null, "AO-01-TRANG-L", "Trắng", "#ffffff", "L", 3, true, null)], []));
    Check(product.Variants.Count == 2 && product.Version.Length > 0, "create product and variants with concurrency token");
    db.MediaAssets.AddRange(new MediaAsset { Url = "https://example.invalid/one.jpg", PublicId = "test/one" },
        new MediaAsset { Url = "https://example.invalid/two.jpg", PublicId = "test/two" });
    await db.SaveChangesAsync();
    var media = await db.MediaAssets.OrderBy(x => x.Id).ToListAsync();
    ProductInput UpdateProduct(ProductView p, int primaryId, int secondaryId) => new(p.Code, p.Name, p.Brand, p.CategoryId, p.Audience, p.Price,
        p.OldPrice, p.Description, p.Material, p.Fit, p.Care, p.IsNew, p.Featured, p.IsVisible, p.SizeChartId, p.Version,
        p.Variants.Select(v => new VariantInput(v.Id, v.Sku, v.Color, v.Hex, v.Size, v.Stock, v.IsActive, v.Version)).ToList(),
        [new(primaryId, 0, true), new(secondaryId, 1, false)]);
    product = await catalog.SaveProductAsync(product.Id, UpdateProduct(product, media[0].Id, media[1].Id));
    product = await catalog.SaveProductAsync(product.Id, UpdateProduct(product, media[1].Id, media[0].Id));
    Check(product.Images.Count == 2 && product.Images[0].MediaAssetId == media[1].Id, "product image order and primary image can be updated");
    Check((await catalog.ProductsAsync(new("ao ca phe", null, null, null, null, null, null, null, null), false)).Total == 1, "accent insensitive product search");
    Check((await catalog.ProductsAsync(new(null, null, null, "L", "Đen", null, null, null, null), false)).Total == 0, "color and size belong to same variant");
    Check((await catalog.ProductsAsync(new(null, null, null, "M", "Đen", null, null, null, null), false)).Total == 1, "available matching variant returned");
    db.StoreSettings.Add(new StoreSettings { Id = 1, ShippingFee = 30000 });
    await db.SaveChangesAsync();
    var orders = new FashionOrders(db);
    var owner = new CartOwner(null, new string('a', 64));
    await orders.SetCartItemAsync(owner, product.Variants[0].Id, 2);
    var quote = new PlaceOrderInput(Guid.NewGuid(), "Người nhận", "customer@example.invalid", "0901234567", "Địa chỉ", "Hà Nội", null,
        30000, [new(product.Variants[0].Id, 2, 200000)]);
    var order = await orders.PlaceOrderAsync(owner, quote);
    Check(order.Total == 430000 && (await orders.GetCartAsync(owner)).Items.Count == 0, "order total includes shipping and cart is cleared");
    Check((await orders.PlaceOrderAsync(owner, quote)).Id == order.Id, "order request is idempotent");
    db.ChangeTracker.Clear();
    Check((await db.ProductVariants.FindAsync(product.Variants[0].Id))!.Stock == 1, "order decrements variant stock");
    await orders.CancelOrderAsync(owner, order.Id);
    await orders.CancelOrderAsync(owner, order.Id);
    db.ChangeTracker.Clear();
    Check((await db.ProductVariants.FindAsync(product.Variants[0].Id))!.Stock == 3, "repeated cancellation restores stock once");
    try { await orders.CancelOrderAsync(new CartOwner(null, new string('b', 64)), order.Id); throw new Exception("other guest accessed order"); }
    catch (CatalogNotFoundException) { Check(true, "another guest cannot access order"); }
    db.ChangeTracker.Clear();
    var variantId = product.Variants[0].Id;
    var last = await db.ProductVariants.SingleAsync(v => v.Id == variantId);
    last.Stock = 1;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var ready = 0;
    var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    async Task<bool> BuyLast(char key)
    {
        await using var otherDb = new FashionContext(options);
        var otherOrders = new FashionOrders(otherDb);
        var otherOwner = new CartOwner(null, new string(key, 64));
        try
        {
            await otherOrders.SetCartItemAsync(otherOwner, variantId, 1);
            if (Interlocked.Increment(ref ready) == 2) bothReady.SetResult();
            await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await otherOrders.PlaceOrderAsync(otherOwner, new(Guid.NewGuid(), "Người nhận", "customer@example.invalid", "0901234567",
                "Địa chỉ", "Hà Nội", null, 30000, [new(variantId, 1, 200000)]));
            return true;
        }
        catch (CatalogValidationException) { return false; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (SqlException e) when (e.Number == 1205) { return false; }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 1205 }) { return false; }
    }
    var attempts = await Task.WhenAll(BuyLast('c'), BuyLast('d'));
    Check(attempts.Count(x => x) == 1 && (await db.ProductVariants.AsNoTracking().SingleAsync(v => v.Id == variantId)).Stock == 0,
        "concurrent orders cannot oversell the last item");
    try { await catalog.DeleteCategoryAsync(category.Id); throw new Exception("category delete unexpectedly succeeded"); }
    catch (CatalogValidationException) { Check(true, "category with products cannot be deleted"); }
    try { await catalog.DeleteProductAsync(product.Id, new byte[8]); throw new Exception("stale version unexpectedly succeeded"); }
    catch (DbUpdateConcurrencyException) { Check(true, "stale product version is rejected"); }
    await catalog.DeleteProductAsync(product.Id, product.Version);
    Check(await catalog.ProductAsync(product.Id, false) is null, "deleted product is hidden from storefront");
    await catalog.DeleteCategoryAsync(category.Id);
    Check(!(await catalog.CategoriesAsync(true)).Any(c => c.Id == category.Id), "category can be removed after its product is deleted while orders remain");
    Console.WriteLine("Fashion database checks passed.");
}
finally
{
    SqlConnection.ClearAllPools();
    if (created)
    {
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
        await drop.ExecuteNonQueryAsync();
        Console.WriteLine("Temporary fashion test database removed.");
    }
}

static void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException($"FAIL: {message}");
    Console.WriteLine($"PASS: {message}");
}
