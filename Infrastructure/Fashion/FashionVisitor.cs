using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Infrastructure.Data.Fashion;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Fashion;

public sealed class FashionVisitor(IDataProtectionProvider protection)
{
    private const string CookieName = "Xmode.Fashion.Guest";
    private readonly IDataProtector protector = protection.CreateProtector("Xmode.Fashion.Guest.v1");

    public string GuestHash(HttpContext http)
    {
        string? key = null;
        if (http.Request.Cookies.TryGetValue(CookieName, out var cookie))
        {
            try { key = protector.Unprotect(cookie); } catch { /* expired or invalid cookie */ }
        }
        if (key is null || key.Length != 64)
        {
            key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            http.Response.Cookies.Append(CookieName, protector.Protect(key), new CookieOptions
            { HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromDays(90) });
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    public CartOwner Owner(HttpContext http) => int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? new(id, null) : new(null, GuestHash(http));

    public async Task MergeAsync(FashionContext db, int userId, string guestHash, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var guest = await db.Carts.Include(c => c.Items).SingleOrDefaultAsync(c => c.GuestKeyHash == guestHash, ct);
            if (guest is null) return;
            var account = await db.Carts.Include(c => c.Items).SingleOrDefaultAsync(c => c.UserId == userId, ct);
            if (account is null)
            {
                guest.UserId = userId;
                guest.GuestKeyHash = null;
            }
            else
            {
                foreach (var item in guest.Items.ToArray())
                {
                    var existing = account.Items.SingleOrDefault(i => i.VariantId == item.VariantId);
                    if (existing is null) { item.CartId = account.Id; }
                    else { existing.Quantity = checked(existing.Quantity + item.Quantity); db.CartItems.Remove(item); }
                }
                db.ChangeTracker.DetectChanges();
                db.Carts.Remove(guest);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
