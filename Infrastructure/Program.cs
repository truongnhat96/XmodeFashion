using Infrastructure.Data.Fashion;
using Infrastructure.Fashion;
using Infrastructure.Fashion.Chat;
using Infrastructure.Fashion.Payments;
using Infrastructure.Models.Cloudinary;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using UseCase.Fashion;

namespace Infrastructure;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        CloudinaryEnvironment.AddLocalFile(builder);
        var connection = builder.Configuration.GetConnectionString("FashionConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure ConnectionStrings:FashionConnection for the new fashion database.");

        builder.Services.AddControllersWithViews().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()));
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(o => { o.Cookie.HttpOnly = true; o.Cookie.IsEssential = true; o.IdleTimeout = TimeSpan.FromHours(10); });
        builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.LoginPath = "/Login";
            o.AccessDeniedPath = "/Forbidden";
            o.Cookie.Name = "Xmode.Fashion.Auth";
            o.Events.OnRedirectToLogin = ctx => { if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 401; else ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = ctx => { if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 403; else ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddDbContext<FashionContext>(o => o.UseSqlServer(connection, sql => sql.EnableRetryOnFailure()));
        builder.Services.AddScoped<IFashionCatalog, FashionCatalog>();
        builder.Services.AddScoped<IFashionOrders, FashionOrders>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient<FashionPaymentGateway>(client => client.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddScoped<IFashionPaymentGateway>(sp => sp.GetRequiredService<FashionPaymentGateway>());
        builder.Services.AddScoped<IFashionPayments, FashionPayments>();
        builder.Services.AddHostedService<PaymentExpirationWorker>();
        builder.Services.AddScoped<FashionVisitor>();
        builder.Services.AddScoped<FashionErrors>();
        builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection("Groq"));
        builder.Services.AddScoped<IShoppingChatCatalog, ShoppingChatCatalog>();
        builder.Services.AddScoped<IShoppingAssistant, ShoppingAssistant>();
        builder.Services.AddHttpClient<IShoppingLanguageModel, GroqShoppingClient>(client => client.Timeout = TimeSpan.FromSeconds(35));
        builder.Services.AddSingleton<ShoppingChatGate>();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("shopping-chat", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Bạn đã gửi nhiều yêu cầu. Vui lòng thử lại sau một lát." }, token);
            };
        });
        builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

        var app = builder.Build();
        if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapStaticAssets();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
        // Schema creation and admin bootstrap are explicit operations, never startup side effects.
        app.Run();
    }
}
