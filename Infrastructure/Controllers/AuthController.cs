using Infrastructure.Data.Fashion;
using Infrastructure.Fashion;
using Infrastructure.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Infrastructure.Controllers;

public class AuthController(FashionContext db, FashionVisitor visitor) : Controller
{
    [HttpGet("/Login")]
    public IActionResult Login() { ViewBag.Message = TempData["Message"]; return View(); }

    [HttpPost("/Login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Username == model.Username, ct);
        if (user is null || new PasswordHasher<FashionUser>().VerifyHashedPassword(user, user.PasswordHash, model.Password ?? "") == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("LoginError", "Tên người dùng hoặc mật khẩu không chính xác.");
            return View(model);
        }
        var guestHash = visitor.GuestHash(HttpContext);
        await visitor.MergeAsync(db, user.Id, guestHash, ct);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Sid, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role.Name) };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = model.IsRememberLoginInfor });
        return Redirect("/");
    }

    [HttpPost("/Logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(); return Redirect("/"); }

    [HttpGet("/Signup")]
    public IActionResult Signup() => View();

    [HttpPost("/Signup"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Signup(SignupModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var username = model.Username.Trim();
        var email = model.Email.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(model.DisplayName)) ModelState.AddModelError("Username", "Vui lòng nhập đầy đủ thông tin.");
        if (await db.Users.AnyAsync(u => u.Username == username, ct)) ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) ModelState.AddModelError("Email", "Email đã tồn tại.");
        if (!ModelState.IsValid) return View(model);
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == "User", ct);
        if (role is null) { ModelState.AddModelError("", "Cửa hàng chưa được khởi tạo."); return View(model); }
        var user = new FashionUser { Username = username, DisplayName = model.DisplayName.Trim(), Email = email, RoleId = role.Id, PasswordHash = "" };
        user.PasswordHash = new PasswordHasher<FashionUser>().HashPassword(user, model.Password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { ModelState.AddModelError("", "Tên đăng nhập hoặc email đã tồn tại."); return View(model); }
        TempData["Message"] = "Đăng ký thành công. Đăng nhập ngay!";
        return Redirect("/Login");
    }

    [HttpGet("/Forbidden")]
    public IActionResult Forbidden() => View();
}
