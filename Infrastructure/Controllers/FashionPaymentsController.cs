using System.Security.Claims;
using System.Text.Json;
using Infrastructure.Data.Fashion;
using Infrastructure.Fashion;
using Infrastructure.Fashion.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UseCase.Fashion;

namespace Infrastructure.Controllers;

[ApiController, Route("api/xmode"), AutoValidateAntiforgeryToken, ServiceFilter(typeof(FashionErrors))]
public sealed class FashionPaymentsController(IFashionPayments payments, FashionPaymentGateway gateway, FashionVisitor visitor, FashionContext db) : ControllerBase
{
    [HttpGet("payment-methods")]
    public IReadOnlyList<PaymentMethodView> Methods() => gateway.Methods();

    [HttpPost("orders/{id:guid}/payment")]
    public async Task<PaymentLink> Start(Guid id, CancellationToken ct)
    {
        var owner = visitor.Owner(HttpContext);
        if (owner.UserId is not null && !await db.Orders.AnyAsync(x => x.Id == id && x.UserId == owner.UserId, ct)) owner = new(null, visitor.GuestHash(HttpContext));
        return await payments.StartAsync(owner, id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", ct);
    }

    // Browser returns only navigate to the order. Only a signed server notification
    // can update the payment status, independently of any customer cookie.
    [HttpGet("payments/vnpay/return")]
    public IActionResult VnpayReturn() => Return(gateway.VerifyVnpay(Request.Query));
    [HttpGet("payments/momo/return")]
    public IActionResult MomoReturn() => Request.Query.Any(q => q.Value.Count != 1) ? Return(null)
        : Return(gateway.VerifyMomo(Request.Query.ToDictionary(k => k.Key, k => k.Value.ToString())));
    private IActionResult Return(PaymentNotification? n) => Redirect(n is null ? "/xmode/orders?payment=invalid" : $"/xmode/orders/{n.Reference}?payment=return");

    [HttpGet("payments/vnpay/ipn")]
    public async Task<IActionResult> VnpayIpn(CancellationToken ct)
    {
        var n = gateway.VerifyVnpay(Request.Query);
        var result = n is null ? new PaymentReceipt("97") : await payments.NotifyAsync(n, ct);
        // VNPay requires these exact property names, rather than MVC's camelCase defaults.
        return new JsonResult(new Dictionary<string, string> { ["RspCode"] = result.Code, ["Message"] = result.Code switch {
            "00" => "Confirm Success", "02" => "Order already confirmed", "01" => "Order not found", "04" => "Invalid amount", _ => "Invalid signature or transaction" } });
    }

    [HttpPost("payments/momo/ipn"), IgnoreAntiforgeryToken, RequestSizeLimit(16384)]
    public async Task<IActionResult> MomoIpn([FromBody] JsonElement body, CancellationToken ct)
    {
        if (body.ValueKind != JsonValueKind.Object) return BadRequest();
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string[] signedFields = ["amount", "extraData", "message", "orderId", "orderInfo", "orderType", "partnerCode", "payType", "requestId", "responseTime", "resultCode", "transId", "signature"];
        foreach (var property in body.EnumerateObject())
        {
            if (!seen.Add(property.Name)) return BadRequest();
            // MoMo may attach optional fee/promotion objects; the canonical HMAC
            // covers the documented fields, independently of those extra fields.
            if (!signedFields.Contains(property.Name, StringComparer.Ordinal)) continue;
            if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) ||
                !data.TryAdd(property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText())) return BadRequest();
        }
        var n = gateway.VerifyMomo(data); if (n is null) return BadRequest();
        var receipt = await payments.NotifyAsync(n, ct);
        return receipt.Code is "00" or "02" ? NoContent() : BadRequest();
    }

    public sealed record RefundInput(byte[] Version, string Reference);
    [Authorize(Roles = "Admin"), HttpPost("admin/orders/{id:guid}/refund-confirmation")]
    public async Task<IActionResult> Refund(Guid id, RefundInput input, CancellationToken ct)
    {
        var actor = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        await payments.MarkRefundedAsync(id, input.Version, input.Reference, actor, ct); return NoContent();
    }
}
