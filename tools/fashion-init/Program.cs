using System.Text.Json;
using Infrastructure.Data.Fashion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Explicit initialization only: no code in the web host runs this operation.
var configFile = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "appsettings.json");
var environmentConnection = Environment.GetEnvironmentVariable("ConnectionStrings__FashionConnection");
using var json = JsonDocument.Parse(await File.ReadAllTextAsync(configFile));
var connection = environmentConnection ?? json.RootElement.GetProperty("ConnectionStrings").GetProperty("FashionConnection").GetString();
var builder = new SqlConnectionStringBuilder(connection);
var testDatabase = args.Contains("--test") && System.Text.RegularExpressions.Regex.IsMatch(builder.InitialCatalog, "^XmodeFashion_E2E_[0-9a-f]{32}$");
if (builder.InitialCatalog != "XmodeFashion" && !testDatabase) throw new InvalidOperationException("Chỉ hỗ trợ khởi tạo database đích XmodeFashion. Hãy kiểm tra connection string.");
var schemaOnly = args.Contains("--schema-only");
var adminName = Environment.GetEnvironmentVariable("XMODE_ADMIN_USER");
var adminEmail = Environment.GetEnvironmentVariable("XMODE_ADMIN_EMAIL");
var adminPassword = Environment.GetEnvironmentVariable("XMODE_ADMIN_PASSWORD");
if (!schemaOnly)
{
    if (string.IsNullOrWhiteSpace(adminName)) { Console.Write("Tên đăng nhập Admin: "); adminName = Console.ReadLine(); }
    if (string.IsNullOrWhiteSpace(adminEmail)) { Console.Write("Email Admin: "); adminEmail = Console.ReadLine(); }
    if (string.IsNullOrWhiteSpace(adminPassword))
    {
        Console.Write("Mật khẩu Admin (tối thiểu 10 ký tự): "); adminPassword = ReadPassword(); Console.WriteLine();
        Console.Write("Nhập lại mật khẩu: "); var confirmation = ReadPassword(); Console.WriteLine();
        if (adminPassword != confirmation) throw new InvalidOperationException("Mật khẩu xác nhận không khớp.");
    }
    if (string.IsNullOrWhiteSpace(adminName) || adminName.Length > 100 || string.IsNullOrWhiteSpace(adminEmail) || adminEmail.Length > 100 ||
        !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(adminEmail) || adminPassword.Length < 10)
        throw new InvalidOperationException("Thông tin Admin không hợp lệ.");
}

var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
await using var master = new SqlConnection(masterBuilder.ConnectionString);
await master.OpenAsync();
await using var existsCommand = master.CreateCommand();
existsCommand.CommandText = "SELECT DB_ID(@name)";
existsCommand.Parameters.AddWithValue("@name", builder.InitialCatalog);
var existsValue = await existsCommand.ExecuteScalarAsync();
var exists = existsValue is not null and not DBNull;
if (!exists)
{
    // The target name is restricted above to the fixed production name or a GUID test name.
    // Explicit creation fails if another database appears between inspection and creation.
    await using var create = master.CreateCommand();
    create.CommandText = $"CREATE DATABASE [{builder.InitialCatalog}]";
    await create.ExecuteNonQueryAsync();
}
int tableCount;
await using (var target = new SqlConnection(builder.ConnectionString))
{
    await target.OpenAsync();
    await using var inspect = target.CreateCommand();
    inspect.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
    tableCount = Convert.ToInt32(await inspect.ExecuteScalarAsync());
}
await using var db = new FashionContext(new DbContextOptionsBuilder<FashionContext>().UseSqlServer(builder.ConnectionString, o => o.EnableRetryOnFailure()).Options);
if (tableCount == 0) await db.Database.MigrateAsync();
else
{
    try
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var expected = db.Database.GetMigrations().ToArray();
        if (!applied.SequenceEqual(expected)) throw new InvalidOperationException("Lịch sử migration không khớp schema thời trang.");
    }
    catch (Exception e) { throw new InvalidOperationException("Database đã có schema không tương thích; không thay đổi dữ liệu.", e); }
}
if (schemaOnly) { Console.WriteLine("Schema thời trang đã sẵn sàng. Danh mục và sản phẩm không được tạo tự động."); return; }
if (await db.Users.AnyAsync() || await db.Roles.AnyAsync()) throw new InvalidOperationException("Database đã có tài khoản hoặc vai trò; không tạo đè Admin.");
var adminRole = new FashionRole { Name = "Admin" };
var userRole = new FashionRole { Name = "User" };
db.Roles.AddRange(adminRole, userRole);
var user = new FashionUser { Username = adminName!.Trim(), DisplayName = adminName.Trim(), Email = adminEmail!.Trim(), PasswordHash = "", Role = adminRole };
user.PasswordHash = new PasswordHasher<FashionUser>().HashPassword(user, adminPassword!);
db.Users.Add(user);
await db.SaveChangesAsync();
Console.WriteLine("Đã khởi tạo schema và tài khoản Admin. Danh mục và sản phẩm đang trống.");

static string ReadPassword()
{
    if (Console.IsInputRedirected) throw new InvalidOperationException("Hãy nhập mật khẩu từ terminal hoặc cấu hình XMODE_ADMIN_PASSWORD.");
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; }
        else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
    }
    return password.ToString();
}
