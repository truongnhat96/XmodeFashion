using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infrastructure.PaymentSupport.VnPay.Lib;
using UseCase.Fashion;

namespace Infrastructure.Fashion.Payments;

public sealed class FashionPaymentGateway(IConfiguration config, HttpClient http, IHttpContextAccessor? contextAccessor = null) : IFashionPaymentGateway
{
    private string Value(string key) => config[key]?.Trim() ?? "";

    private bool Has(params string[] keys) => keys.All(k => !string.IsNullOrWhiteSpace(Value(k)) && !Value(k).Contains("your_", StringComparison.OrdinalIgnoreCase));
    private string BaseUrl {
        get {
            var configured = Value("Payments:PublicBaseUrl");
            if (!string.IsNullOrEmpty(configured)) return configured;
            var request = contextAccessor?.HttpContext?.Request;
            return request is null ? "" : $"{request.Scheme}://{request.Host}";
        }
    }
    private bool BaseReady => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var u)
        && (u.Scheme == "https" || (u.Scheme == "http" && u.IsLoopback)) && u.AbsolutePath == "/" && u.Query == "" && u.Fragment == "" && u.UserInfo == "";
    private string Callback(string route) => BaseUrl.TrimEnd('/') + "/api/xmode/payments/" + route;
    public IReadOnlyList<PaymentMethodView> Methods() => [new("COD", "Thanh toán khi nhận hàng (COD)", true, null),
        Method("VNPay", "VNPay · Thẻ ngân hàng / QR", BaseReady && Has("Vnpay:TmnCode", "Vnpay:HashSecret") && ValidVnpay(Value("Vnpay:BaseUrl"))),
        Method("MoMo", "Ví MoMo", BaseReady && Has("MomoAPI:PartnerCode", "MomoAPI:AccessKey", "MomoAPI:SecretKey") && ValidMomo(Value("MomoAPI:Endpoint")))];
    private static PaymentMethodView Method(string code, string name, bool ready) => new(code, name, ready,
        ready ? null : "Chưa được cấu hình hoặc đang tạm ngưng. Vui lòng chọn phương thức khác.");
    private static bool ValidVnpay(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && u.UserInfo == ""
        && (u.Host == "sandbox.vnpayment.vn" || u.Host == "pay.vnpay.vn");
    private static bool ValidMomo(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && u.UserInfo == ""
        && (u.Host == "test-payment.momo.vn" || u.Host == "payment.momo.vn");
    public static bool ValidPayUrl(string provider, string url) => provider == "VNPay" ? ValidVnpay(url) : provider == "MoMo" && ValidMomo(url);

    public async Task<string> CreateAsync(PaymentRequest p, string ipAddress, CancellationToken ct)
    {
        if (!Methods().Any(m => m.Code == p.Provider && m.Available)) throw new PaymentGatewayException("Cổng thanh toán chưa sẵn sàng.");
        if (p.Amount != decimal.Truncate(p.Amount) || p.Amount < 5000 || p.Amount > 50000000 || p.ExpiresAt <= DateTime.UtcNow)
            throw new CatalogValidationException("Số tiền hoặc thời hạn thanh toán không hợp lệ.");
        if (p.Provider == "VNPay")
        {
            var pay = new VnPayLibrary();
            // VNPay's timestamp is Vietnam time on Windows and Linux alike.
            string Date(DateTime utc) => utc.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            pay.AddRequestData("vnp_Version", "2.1.0"); pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", Value("Vnpay:TmnCode"));
            pay.AddRequestData("vnp_Amount", (p.Amount * 100).ToString("0", CultureInfo.InvariantCulture));
            pay.AddRequestData("vnp_CreateDate", Date(DateTime.UtcNow)); pay.AddRequestData("vnp_ExpireDate", Date(p.ExpiresAt));
            pay.AddRequestData("vnp_CurrCode", "VND"); pay.AddRequestData("vnp_Locale", "vn");
            pay.AddRequestData("vnp_IpAddr", IPAddress.TryParse(ipAddress, out var ip) ? (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString() : "127.0.0.1");
            pay.AddRequestData("vnp_OrderInfo", "Thanh toan don " + p.Code); pay.AddRequestData("vnp_OrderType", "fashion");
            pay.AddRequestData("vnp_ReturnUrl", Callback("vnpay/return")); pay.AddRequestData("vnp_TxnRef", p.Id.ToString("N"));
            return pay.CreateRequestUrl(Value("Vnpay:BaseUrl"), Value("Vnpay:HashSecret"));
        }
        var id = p.Id.ToString("N"); var amount = p.Amount.ToString("0", CultureInfo.InvariantCulture);
        var info = "Thanh toan don " + p.Code;
        var raw = $"accessKey={Value("MomoAPI:AccessKey")}&amount={amount}&extraData=&ipnUrl={Callback("momo/ipn")}&orderId={id}&orderInfo={info}&partnerCode={Value("MomoAPI:PartnerCode")}&redirectUrl={Callback("momo/return")}&requestId={id}&requestType=captureWallet";
        try
        {
            using var response = await http.PostAsJsonAsync(Value("MomoAPI:Endpoint"), new {
                partnerCode = Value("MomoAPI:PartnerCode"), requestId = id, amount = (long)p.Amount, orderId = id, orderInfo = info,
                redirectUrl = Callback("momo/return"), ipnUrl = Callback("momo/ipn"), requestType = "captureWallet", extraData = "",
                autoCapture = true, lang = "vi", signature = Sign(raw, Value("MomoAPI:SecretKey"), false)
            }, ct);
            if (!response.IsSuccessStatusCode) throw new PaymentGatewayException("MoMo chưa trả được liên kết thanh toán. Đơn đã được lưu; vui lòng kiểm tra lại trong đơn hàng.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var data = json.RootElement;
            if (!data.TryGetProperty("resultCode", out var result) || !result.TryGetInt32(out var code) || code != 0 ||
                !data.TryGetProperty("orderId", out var oid) || oid.GetString() != id ||
                !data.TryGetProperty("requestId", out var rid) || rid.GetString() != id ||
                !data.TryGetProperty("payUrl", out var link) || link.GetString() is not { } url || url.Length > 2000 || !ValidMomo(url))
                throw new PaymentGatewayException("MoMo chưa tạo được liên kết. Đơn vẫn được lưu để chờ xác nhận hoặc tự hết hạn; không gửi lại bằng mã giao dịch mới.");
            return url;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PaymentGatewayException("MoMo phản hồi chậm. Hãy kiểm tra lại đơn hàng trước khi thao tác tiếp."); }
        catch (HttpRequestException) { throw new PaymentGatewayException("Không kết nối được MoMo. Hãy kiểm tra lại đơn hàng sau một lát."); }
        catch (JsonException) { throw new PaymentGatewayException("Phản hồi MoMo không hợp lệ. Đơn hàng vẫn được lưu."); }
    }

    public PaymentNotification? VerifyVnpay(IQueryCollection query)
    {
        if (!Has("Vnpay:HashSecret", "Vnpay:TmnCode") || query.Any(q => q.Value.Count != 1) || query.Count > 40) return null;
        var data = query.Where(k => k.Key.StartsWith("vnp_", StringComparison.Ordinal) && k.Key is not ("vnp_SecureHash" or "vnp_SecureHashType") && !string.IsNullOrEmpty(k.Value))
            .OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => WebUtility.UrlEncode(k.Key) + "=" + WebUtility.UrlEncode(k.Value.ToString()));
        if (!EqualHash(query["vnp_SecureHash"].ToString(), Sign(string.Join("&", data), Value("Vnpay:HashSecret"), true)) ||
            query["vnp_TmnCode"] != Value("Vnpay:TmnCode") || !Guid.TryParseExact(query["vnp_TxnRef"], "N", out var id) ||
            !long.TryParse(query["vnp_Amount"], NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0 || amount % 100 != 0 ||
            string.IsNullOrEmpty(query["vnp_ResponseCode"]) || string.IsNullOrEmpty(query["vnp_TransactionStatus"])) return null;
        return new("VNPay", id, amount / 100m, query["vnp_TransactionNo"].ToString(), query["vnp_ResponseCode"] == "00" && query["vnp_TransactionStatus"] == "00");
    }

    public PaymentNotification? VerifyMomo(IReadOnlyDictionary<string, string> data)
    {
        string Get(string k) => data.GetValueOrDefault(k) ?? "";
        string[] fields = ["amount", "extraData", "message", "orderId", "orderInfo", "orderType", "partnerCode", "payType", "requestId", "responseTime", "resultCode", "transId"];
        if (!Has("MomoAPI:SecretKey", "MomoAPI:AccessKey", "MomoAPI:PartnerCode") || fields.Any(k => !data.ContainsKey(k))) return null;
        var raw = "accessKey=" + Value("MomoAPI:AccessKey") + "&" + string.Join("&", fields.Select(k => k + "=" + Get(k)));
        if (!EqualHash(Get("signature"), Sign(raw, Value("MomoAPI:SecretKey"), false)) || Get("partnerCode") != Value("MomoAPI:PartnerCode") ||
            Get("orderId") != Get("requestId") || !Guid.TryParseExact(Get("orderId"), "N", out var id) ||
            !long.TryParse(Get("amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0 ||
            !int.TryParse(Get("resultCode"), out var code)) return null;
        return new("MoMo", id, amount, Get("transId"), code == 0, code is 1000 or 7000 or 7002 or 9000);
    }
    public static string Sign(string raw, string secret, bool sha512) => Convert.ToHexString(sha512
        ? HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw))
        : HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    private static bool EqualHash(string actual, string expected)
    {
        try { return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expected)); }
        catch (FormatException) { return false; }
    }
}
