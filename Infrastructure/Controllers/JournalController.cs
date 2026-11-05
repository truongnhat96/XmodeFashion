using System.Security.Claims;
using Infrastructure.Data.Fashion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Controllers;

public class JournalController(FashionContext db) : Controller
{
    [HttpGet("/Posts"), HttpGet("/BlogList")]
    public async Task<IActionResult> Index(string? keyword, CancellationToken ct)
    {
        if (Request.Path == "/BlogList" && !User.IsInRole("Admin")) return Forbid();
        var posts = db.Posts.AsNoTracking().Include(p => p.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword)) posts = posts.Where(p => p.Title.Contains(keyword));
        return View(await posts.OrderByDescending(p => p.CreatedAt).ToListAsync(ct));
    }
    [HttpGet("/Post/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    { var post = await db.Posts.AsNoTracking().Include(p => p.User).SingleOrDefaultAsync(p => p.Id == id, ct); return post is null ? NotFound() : View(post); }
    [Authorize(Roles = "Admin"), HttpGet("/Article"), HttpGet("/Article/{id:guid}")]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        var post = id is null ? new FashionPost { Id = Guid.Empty, Title = "", Content = "" } : await db.Posts.FindAsync([id.Value], ct);
        return post is null ? NotFound() : View(post);
    }
    [Authorize(Roles = "Admin"), HttpPost("/Article"), HttpPost("/Article/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid? id, string title, string content, string? imageUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 100 || string.IsNullOrWhiteSpace(content) || content.Length > 100_000)
        { ModelState.AddModelError("", "Vui lòng nhập tiêu đề, nội dung hợp lệ và chọn ảnh trong thư viện."); return View(new FashionPost { Id = id ?? Guid.Empty, Title = title ?? "", Content = content ?? "", ImageUrl = imageUrl }); }
        var postId = id ?? Guid.NewGuid();
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                // Coordinate with image deletion until the post reference is committed.
                var image = await db.MediaAssets.FromSqlInterpolated($"SELECT * FROM [MediaAssets] WITH (UPDLOCK, HOLDLOCK) WHERE [Url] = {imageUrl}").FirstOrDefaultAsync(ct);
                if (image is null) return "image";
            }
            var post = await db.Posts.FindAsync([postId], ct);
            if (post is null && id is not null) return "missing";
            if (post is null)
            {
                post = new FashionPost { Id = postId, Title = title, Content = content, UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), CreatedAt = DateTime.UtcNow };
                db.Posts.Add(post);
            }
            post.Title = title.Trim(); post.Content = content.Trim(); post.ImageUrl = imageUrl;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "ok";
        });
        if (result == "missing") return NotFound();
        if (result == "image") { ModelState.AddModelError("", "Ảnh không tồn tại trong thư viện."); return View(new FashionPost { Id = id ?? Guid.Empty, Title = title, Content = content, ImageUrl = imageUrl }); }
        return Redirect("/BlogList");
    }
    [Authorize(Roles = "Admin"), HttpPost("/Article/{id:guid}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { var post = await db.Posts.FindAsync([id], ct); if (post is null) return NotFound(); db.Posts.Remove(post); await db.SaveChangesAsync(ct); return Redirect("/BlogList"); }
}
