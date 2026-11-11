using System.Text.Json;
using Infrastructure.Fashion.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UseCase.Fashion;

namespace Infrastructure.Controllers;

[ApiController, Route("api/xmode/chat"), AutoValidateAntiforgeryToken, EnableRateLimiting("shopping-chat")]
public sealed class FashionChatController(IShoppingAssistant assistant, IShoppingLanguageModel model, IShoppingChatCatalog catalog, ShoppingChatGate gate) : ControllerBase
{
    private const string HistoryKey = "Xmode.ShoppingChat.v1";
    public sealed record ChatRequest(string Message, int? ProductId);
    public sealed record SavedTurn(string Role, string Content, ShoppingChatReply? Reply);
    private List<SavedTurn> History()
    {
        var json = HttpContext.Session.GetString(HistoryKey);
        return json is null ? [] : JsonSerializer.Deserialize<List<SavedTurn>>(json) ?? [];
    }
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        HttpContext.Session.SetString("Xmode.Chat.Ready", "1");
        var turns = History();
        var ids = turns.Where(t => t.Reply != null).SelectMany(t => t.Reply!.Products.Select(p => p.Id)).Distinct().ToArray();
        var current = await catalog.GetProductsAsync(ids, ct);
        var refreshed = turns.Select(t => t.Reply is null ? t : t with { Reply = t.Reply with
        {
            Products = t.Reply.Products.Where(p => current.Any(c => c.Id == p.Id)).Select(p =>
            {
                var item = current.Single(c => c.Id == p.Id);
                return new ShoppingProductCard(item.Id, item.Name, item.Price, item.OldPrice, item.ImageUrl, $"/xmode/product/{item.Id}", item.Variants);
            }).ToList()
        } }).ToList();
        return Ok(new { available = model.IsConfigured, turns = refreshed });
    }
    [HttpPost, RequestSizeLimit(8_192)]
    public async Task<IActionResult> Post(ChatRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 1200)
            return BadRequest(new { message = "Vui lòng nhập câu hỏi từ 1 đến 1.200 ký tự." });
        var session = HttpContext.Session.Id;
        if (!gate.TryEnter(session)) return Conflict(new { message = "Một câu hỏi đang được xử lý. Bạn vui lòng chờ một lát." });
        try
        {
            var turns = History();
            var history = turns.TakeLast(8).Select(t => new ShoppingChatTurn(t.Role, t.Content)).ToList();
            var reply = await assistant.ReplyAsync(input.Message, input.ProductId, history, ct);
            turns.Add(new("user", input.Message.Trim(), null)); turns.Add(new("assistant", reply.Message, reply));
            HttpContext.Session.SetString(HistoryKey, JsonSerializer.Serialize(turns.TakeLast(12)));
            return Ok(reply);
        }
        catch (ShoppingChatUnavailableException e) { return StatusCode(e.StatusCode, new { message = e.Message }); }
        catch (CatalogValidationException e) { return BadRequest(new { message = e.Message }); }
        finally { gate.Leave(session); }
    }
    [HttpDelete]
    public IActionResult Clear()
    {
        var session = HttpContext.Session.Id;
        if (!gate.TryEnter(session)) return Conflict(new { message = "Vui lòng chờ câu trả lời hiện tại trước khi bắt đầu cuộc trò chuyện mới." });
        try { HttpContext.Session.Remove(HistoryKey); return NoContent(); }
        finally { gate.Leave(session); }
    }
}
