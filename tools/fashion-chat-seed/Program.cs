using System.Text;
using System.Text.Json;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FashionChatSeed;
using Infrastructure.Data.Fashion;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

Console.OutputEncoding = Encoding.UTF8;
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var manifest = JsonSerializer.Deserialize<SeedManifest>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "catalog.json")), jsonOptions)!;
string Argument(string name, string? fallback = null)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback ?? throw new ArgumentException($"Missing {name}");
}
var root = Path.GetFullPath(Argument("--root", Directory.GetCurrentDirectory()));
var file = Path.GetFullPath(Argument("--file", Path.Combine(root, "docs", "database", "xmode-chatbot-products-20261110.sql")));
if (args.Contains("--prepare"))
{
    var secrets = ReadLocalSettings(root);
    string Secret(string key) => Environment.GetEnvironmentVariable(key) ?? secrets.GetValueOrDefault(key) ?? throw new InvalidOperationException($"Configure {key} locally.");
    var cloud = new Cloudinary(new Account(Secret("CloudinarySettings__CloudName"), Secret("CloudinarySettings__ApiKey"), Secret("CloudinarySettings__ApiSecret")));
    var media = new Dictionary<string, ExportMedia>();
    foreach (var category in manifest.Categories)
    {
        var path = Path.Combine(root, "Infrastructure", "wwwroot", "images", "xmode", category.Image);
        var publicId = $"xmode-fashion/acceptance-20261110/{category.Key}";
        var uploaded = await cloud.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(path), PublicId = publicId, Overwrite = false,
            UniqueFilename = false, UseFilename = false
        });
        if (uploaded.Error != null || uploaded.SecureUrl is null || string.IsNullOrEmpty(uploaded.PublicId))
            throw new InvalidOperationException($"Could not prepare category image: {category.Key}. Provider details are not logged.");
        media.Add(category.Key, new(uploaded.SecureUrl.ToString(), uploaded.PublicId, $"Ảnh minh họa {category.Name}", DateTime.UtcNow));
        Console.WriteLine($"Prepared image: {category.Name}");
    }
    var charts = manifest.Charts.Select(c => new ExportChart(c.Name,c.Instructions,c.Rows.Select((r,i)=>new ExportRow(r.Size,r.Chest,r.Waist,r.Hip,i)).ToList())).ToList();
    var products = manifest.Products.Select((p, index) =>
    {
        var category = manifest.Categories.Single(c => c.Key == p.Category);
        var chart = manifest.Charts.Single(c => c.Key == p.Chart);
        var variants = p.Colors.SelectMany((color, colorIndex) => chart.Rows.Select(row => new ExportVariant(
            $"{p.Code}-{colorIndex + 1}-{row.Size}", color.Name, color.Hex, row.Size,
            index == 0 && colorIndex == 1 && row.Size == "L" ? 0 : p.Stock, true))).ToList();
        return new ExportProduct(p.Code,p.Name,"Xmode",category.Name,p.Audience,p.Price,p.OldPrice,p.Description,p.Material,p.Fit,p.Care,
            true,index is 0 or 2 or 6 or 8,true,chart.Name,DateTime.UtcNow,DateTime.UtcNow,variants,[new(media[p.Category].PublicId,0,true)]);
    }).ToList();
    var bundle = new CatalogBundle(manifest.Categories.Select((c,i)=>new ExportCategory(c.Name,c.Description,media[c.Key].PublicId,i,true)).ToList(),charts,media.Values.ToList(),products);
    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
    await File.WriteAllTextAsync(file, CatalogExport.Build(bundle), new UTF8Encoding(false));
    Console.WriteLine($"Prepared SQL: {file} (10 products / 60 variants / 5 categories / 3 size charts).");
}
else if (args.Contains("--apply-main"))
{
    using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "Infrastructure", "appsettings.json")));
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__FashionConnection") ??
        config.RootElement.GetProperty("ConnectionStrings").GetProperty("FashionConnection").GetString();
    var builder = new SqlConnectionStringBuilder(connection);
    if (builder.InitialCatalog != "XmodeFashion") throw new InvalidOperationException("Main seed only targets the configured XmodeFashion database.");
    await using var db = new FashionContext(new DbContextOptionsBuilder<FashionContext>().UseSqlServer(builder.ConnectionString).Options);
    if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Apply current fashion migrations explicitly before seeding.");
    var sql = await File.ReadAllTextAsync(file);
    ValidateSql(sql);
    await db.Database.ExecuteSqlRawAsync(sql);
    var bundle = await CatalogExport.ReadAsync(db, manifest.Products.Select(p=>p.Code).ToArray());
    await File.WriteAllTextAsync(file, CatalogExport.Build(bundle), new UTF8Encoding(false));
    Console.WriteLine($"Applied to {builder.InitialCatalog}: {bundle.Products.Count} products, {bundle.Products.Sum(p=>p.Variants.Count)} variants. Exported actual database snapshot: {file}");
}
else if (args.Contains("--verify-export"))
{
    var sql = await File.ReadAllTextAsync(file); ValidateSql(sql);
    var database = $"XmodeFashion_ChatSeedCheck_{Guid.NewGuid():N}";
    var master = new SqlConnectionStringBuilder { DataSource = @".\SQLEXPRESS", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
    var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = database };
    var created = false;
    try
    {
        await using (var connection = new SqlConnection(master.ConnectionString))
        {
            await connection.OpenAsync(); await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{database}]"; await cmd.ExecuteNonQueryAsync(); created = true;
        }
        await using var db = new FashionContext(new DbContextOptionsBuilder<FashionContext>().UseSqlServer(target.ConnectionString).Options);
        await db.Database.MigrateAsync();
        db.Categories.Add(new FashionCategory { Name = "Danh mục có sẵn để kiểm tra ánh xạ ID" }); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(sql); await db.Database.ExecuteSqlRawAsync(sql);
        if (await db.Products.CountAsync() != 10 || await db.ProductVariants.CountAsync() != 60 || await db.Categories.CountAsync() != 6 ||
            await db.MediaAssets.CountAsync() != 5 || await db.SizeCharts.CountAsync() != 3 || await db.Users.AnyAsync() || await db.Orders.AnyAsync())
            throw new InvalidOperationException("Export counts or isolation checks failed.");
        var variant = await db.ProductVariants.FirstAsync(v => v.Stock > 1); variant.Stock = 1; await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(sql); db.ChangeTracker.Clear();
        if ((await db.ProductVariants.SingleAsync(v => v.Id == variant.Id)).Stock != 1)
            throw new InvalidOperationException("Rerunning the seed must not reset stock.");
        Console.WriteLine("PASS SQL import twice, ID remapping, FK relations, Unicode, no account/order data and preservation of existing stock.");
    }
    finally
    {
        SqlConnection.ClearAllPools();
        if (created)
        {
            await using var connection = new SqlConnection(master.ConnectionString); await connection.OpenAsync();
            await using var cmd = connection.CreateCommand(); cmd.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
            await cmd.ExecuteNonQueryAsync(); Console.WriteLine("Temporary seed-verification database removed.");
        }
    }
}
else throw new ArgumentException("Choose --prepare, --apply-main or --verify-export; optional --root and --file.");

static Dictionary<string,string> ReadLocalSettings(string root)
{
    var values = new Dictionary<string,string>(StringComparer.Ordinal);
    var path = Path.Combine(root,".env");
    if (!File.Exists(path)) return values;
    foreach (var raw in File.ReadLines(path))
    {
        var line = raw.Trim().TrimStart('\uFEFF'); var split=line.IndexOf('=');
        if (line.StartsWith('#') || split <= 0) continue;
        var key=line[..split].Trim(); var value=line[(split+1)..].Trim();
        if (value.Length>=2 && ((value[0]=='"' && value[^1]=='"') || (value[0]=='\'' && value[^1]=='\''))) value=value[1..^1];
        if (key.StartsWith("CloudinarySettings__",StringComparison.Ordinal)) values[key]=value;
    }
    return values;
}
static void ValidateSql(string sql)
{
    if (!sql.StartsWith("-- XMODE fashion chatbot acceptance catalog",StringComparison.Ordinal) ||
        System.Text.RegularExpressions.Regex.IsMatch(sql, @"\b(UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|MERGE)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        throw new InvalidOperationException("Only the generated insert-only acceptance catalog may be applied.");
}
