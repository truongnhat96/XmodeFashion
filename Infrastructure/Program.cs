using Infrastructure.Data.Fashion;
using Infrastructure.Fashion;
using Infrastructure.Models.Cloudinary;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
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

        builder.Services.AddControllersWithViews();
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
        builder.Services.AddScoped<FashionVisitor>();
        builder.Services.AddScoped<FashionErrors>();
        builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

        var app = builder.Build();
        if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapStaticAssets();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
        // Schema creation and admin bootstrap are explicit operations, never startup side effects.
        app.Run();
    }
}
