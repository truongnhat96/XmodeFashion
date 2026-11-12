using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infrastructure.Data.Fashion;
using Infrastructure.Fashion.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using UseCase.Fashion;

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["Payments:PublicBaseUrl"]="https://shop.example.invalid", ["Vnpay:Enabled"]="true", ["Vnpay:TmnCode"]="TESTCODE",
    ["Vnpay:HashSecret"]="fixture-vnpay-secret", ["Vnpay:BaseUrl"]="https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    ["MomoAPI:Enabled"]="true", ["MomoAPI:PartnerCode"]="TESTPARTNER", ["MomoAPI:AccessKey"]="fixture-access",
    ["MomoAPI:SecretKey"]="fixture-momo-secret", ["MomoAPI:Endpoint"]="https://test-payment.momo.vn/v2/gateway/api/create"
}).Build();
var handler = new StubHandler(); var gateway = new FashionPaymentGateway(config, new HttpClient(handler));
Check(gateway.Methods().All(m=>m.Available), "COD and both configured sandbox providers are available");
var p = new PaymentRequest(Guid.NewGuid(), Guid.NewGuid(), "XM-CHECK", "VNPay", 230000, DateTime.UtcNow.AddMinutes(15));
var vnpUrl = await gateway.CreateAsync(p, "127.0.0.1", default);
var vnpQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(vnpUrl).Query);
Check(vnpQuery["vnp_Amount"]=="23000000" && vnpQuery["vnp_TxnRef"]==p.Id.ToString("N") && vnpQuery["vnp_ReturnUrl"]=="https://shop.example.invalid/api/xmode/payments/vnpay/return", "VNPay uses authoritative integer amount, persisted reference and new return route");
string VnpSign(Dictionary<string,StringValues> values) {
    var raw=string.Join("&",values.Where(k=>k.Key!="vnp_SecureHash").OrderBy(k=>k.Key,StringComparer.Ordinal).Select(k=>WebUtility.UrlEncode(k.Key)+"="+WebUtility.UrlEncode(k.Value)));
    return Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes("fixture-vnpay-secret"),Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
var response = new Dictionary<string,StringValues> { ["vnp_TmnCode"]="TESTCODE",["vnp_TxnRef"]=p.Id.ToString("N"),["vnp_Amount"]="23000000",["vnp_ResponseCode"]="00",["vnp_TransactionStatus"]="00",["vnp_TransactionNo"]="12345" };
response["vnp_SecureHash"]=VnpSign(response);
Check(gateway.VerifyVnpay(new QueryCollection(response)) is {Success:true,Amount:230000}, "VNPay validates signed transaction success");
response["vnp_Amount"]="1"; Check(gateway.VerifyVnpay(new QueryCollection(response)) is null,"VNPay rejects tampered amount/signature");
response["vnp_Amount"]="23000000";response["vnp_TxnRef"]=new StringValues([p.Id.ToString("N"),p.Id.ToString("N")]);
Check(gateway.VerifyVnpay(new QueryCollection(response)) is null,"VNPay rejects duplicate query fields");

var momo=await gateway.CreateAsync(p with {Provider="MoMo"},"127.0.0.1",default);
using (var sent=JsonDocument.Parse(handler.LastBody!)) {
    var body=sent.RootElement;
    Check(body.GetProperty("requestType").GetString()=="captureWallet" && body.GetProperty("ipnUrl").GetString()!.EndsWith("/momo/ipn") && body.GetProperty("amount").GetInt64()==230000 && body.GetProperty("autoCapture").GetBoolean(),"MoMo v2 uses captureWallet, IPN, correct amount and auto capture");
    var raw=$"accessKey=fixture-access&amount=230000&extraData=&ipnUrl=https://shop.example.invalid/api/xmode/payments/momo/ipn&orderId={p.Id:N}&orderInfo=Thanh toan don XM-CHECK&partnerCode=TESTPARTNER&redirectUrl=https://shop.example.invalid/api/xmode/payments/momo/return&requestId={p.Id:N}&requestType=captureWallet";
    Check(body.GetProperty("signature").GetString()==Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("fixture-momo-secret"),Encoding.UTF8.GetBytes(raw))).ToLowerInvariant(),"MoMo create signature follows provider field order");
}
var data=new Dictionary<string,string> { ["amount"]="230000",["extraData"]="",["message"]="Successful.",["orderId"]=p.Id.ToString("N"),["orderInfo"]="Thanh toan don XM-CHECK",["orderType"]="momo_wallet",["partnerCode"]="TESTPARTNER",["payType"]="qr",["requestId"]=p.Id.ToString("N"),["responseTime"]="1794350000000",["resultCode"]="0",["transId"]="555123" };
var momoRaw="accessKey=fixture-access&"+string.Join("&",data.Select(k=>k.Key+"="+k.Value));
data["signature"]=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("fixture-momo-secret"),Encoding.UTF8.GetBytes(momoRaw))).ToLowerInvariant();
Check(gateway.VerifyMomo(data) is {Success:true,TransactionId:"555123"},"MoMo accepts an independently signed IPN");
data["amount"]="1";Check(gateway.VerifyMomo(data) is null,"MoMo rejects tampered IPN");
handler.BadUrl=true;
await Reject<PaymentGatewayException>(()=>gateway.CreateAsync(p with {Provider="MoMo"},"127.0.0.1",default),"MoMo rejects pay URLs outside its hosts");
config["MomoAPI:SecretKey"]="";Check(!gateway.Methods().Single(m=>m.Code=="MoMo").Available,"missing credentials disable provider without hiding it");

var database=$"XmodeFashion_PaymentCheck_{Guid.NewGuid():N}";
var master=new SqlConnectionStringBuilder {DataSource=@".\SQLEXPRESS",InitialCatalog="master",IntegratedSecurity=true,TrustServerCertificate=true,ConnectTimeout=5};
var target=new SqlConnectionStringBuilder(master.ConnectionString){InitialCatalog=database};var created=false;
try {
    await using(var c=new SqlConnection(master.ConnectionString)){await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText=$"CREATE DATABASE [{database}]";await cmd.ExecuteNonQueryAsync();created=true;}
    var options=new DbContextOptionsBuilder<FashionContext>().UseSqlServer(target.ConnectionString,s=>s.EnableRetryOnFailure()).Options;
    await using var db=new FashionContext(options);await db.Database.MigrateAsync();await db.Database.MigrateAsync();
    Check(!(await db.Database.GetPendingMigrationsAsync()).Any(),"payment migration is repeatable on independent SQL database");
    var catalog=new FashionCatalog(db);var category=await catalog.SaveCategoryAsync(null,new("Áo kiểm thử",null,null,0,true));
    var product=await catalog.SaveProductAsync(null,new("PAY-01","Áo thanh toán",null,category.Id,"Unisex",200000,null,"Áo kiểm thử",null,null,null,false,false,true,null,null,[new(null,"PAY-01-M","Đen","#222222","M",30,true,null)],[]));
    db.StoreSettings.Add(new StoreSettings{Id=1,ShippingFee=30000});await db.SaveChangesAsync();
    var owner=new CartOwner(null,new string('c',64));var orders=new FashionOrders(db);var fake=new FakeGateway();var payments=new FashionPayments(db,fake);
    async Task<OrderView> Order(string provider="VNPay") {
        await orders.SetCartItemAsync(owner,product.Variants[0].Id,1);
        return await orders.PlaceOrderAsync(owner,new(Guid.NewGuid(),"Người nhận","test@example.invalid","0901234567","Địa chỉ","Hà Nội",null,30000,[new(product.Variants[0].Id,1,200000)],provider));
    }
    var order=await Order(); Check(order.PaymentStatus=="Pending" && await db.Payments.CountAsync()==1,"online checkout creates persisted pending attempt with reserved stock");
    var first=await payments.StartAsync(owner,order.Id,"127.0.0.1");var again=await payments.StartAsync(owner,order.Id,"127.0.0.1");
    Check(first.Url==again.Url && fake.Calls==1,"repeated payment initiation reuses one reference and one link");
    await Reject<CatalogNotFoundException>(()=>payments.StartAsync(new(null,new string('d',64)),order.Id,"127.0.0.1"),"another guest cannot initiate payment");
    Check((await payments.NotifyAsync(new("VNPay",order.Id,1,"TRAN-A",true))).Code=="04","callback rejects amount mismatching database snapshot");
    Check((await payments.NotifyAsync(new("MoMo",order.Id,230000,"TRAN-A",true))).Code=="01","callback cannot switch provider");
    Check((await payments.NotifyAsync(new("VNPay",order.Id,230000,"TRAN-A",true))).Code=="00","valid callback marks online payment paid");
    Check((await payments.NotifyAsync(new("VNPay",order.Id,230000,"TRAN-A",true))).Code=="02","duplicate success is acknowledged idempotently");
    await payments.NotifyAsync(new("VNPay",order.Id,230000,"0",false)); db.ChangeTracker.Clear();
    Check((await db.Orders.FindAsync(order.Id))!.PaymentStatus=="Paid","failure notification cannot downgrade paid payment");
    await orders.CancelOrderAsync(owner,order.Id);await orders.CancelOrderAsync(owner,order.Id);db.ChangeTracker.Clear();
    var cancelled=(await db.Orders.FindAsync(order.Id))!;
    Check(cancelled.PaymentStatus=="RefundRequired" && (await db.ProductVariants.FindAsync(product.Variants[0].Id))!.Stock==30,"cancelling paid order restores stock once and requires refund");
    await payments.MarkRefundedAsync(order.Id,cancelled.Version,"BANK-REFUND-01",null);db.ChangeTracker.Clear();
    Check((await db.Orders.FindAsync(order.Id))!.PaymentStatus=="Refunded","admin refund confirmation records a reference without a second charge");
    var failed=await Order("MoMo");await payments.NotifyAsync(new("MoMo",failed.Id,230000,"0",false));await payments.NotifyAsync(new("MoMo",failed.Id,230000,"0",false));db.ChangeTracker.Clear();
    Check((await db.Orders.FindAsync(failed.Id))!.Status=="Cancelled" && (await db.ProductVariants.FindAsync(product.Variants[0].Id))!.Stock==30,"failed payment cancels and releases inventory exactly once");
    var expired=await Order();db.ChangeTracker.Clear();var entity=(await db.Orders.FindAsync(expired.Id))!;entity.PaymentExpiresAt=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
    await payments.ExpireAsync();await payments.ExpireAsync();db.ChangeTracker.Clear();
    Check((await db.Orders.FindAsync(expired.Id))!.PaymentStatus=="Expired" && (await db.ProductVariants.FindAsync(product.Variants[0].Id))!.Stock==30,"expired orders release inventory exactly once");
    await payments.NotifyAsync(new("VNPay",expired.Id,230000,"LATE-A",true));db.ChangeTracker.Clear();
    Check((await db.Orders.FindAsync(expired.Id)) is {Status:"Cancelled",PaymentStatus:"RefundRequired"} && (await db.ProductVariants.FindAsync(product.Variants[0].Id))!.Stock==30,"late success never revives order or reserves stock again");
    var cod=await Order("COD");await Reject<CatalogValidationException>(()=>payments.StartAsync(owner,cod.Id,"127.0.0.1"),"COD never creates a gateway link");await orders.CancelOrderAsync(owner,cod.Id);
    // A notification racing expiry/cancellation must not double restore or oversell inventory.
    var racing=await Order();
    await using(var d1=new FashionContext(options)) await using(var d2=new FashionContext(options)) {
        await Task.WhenAll(new FashionOrders(d1).CancelOrderAsync(owner,racing.Id),new FashionPayments(d2,fake).NotifyAsync(new("VNPay",racing.Id,230000,"RACE-A",true)));
    }
    db.ChangeTracker.Clear();Check((await db.Orders.FindAsync(racing.Id)) is {Status:"Cancelled",PaymentStatus:"RefundRequired"} && (await db.ProductVariants.FindAsync(product.Variants[0].Id))!.Stock==30,"concurrent cancellation and payment settle with one inventory restoration");
    Check(!await db.Users.AnyAsync(),"payment checks never require real customer accounts");
} finally {
    SqlConnection.ClearAllPools();if(created){await using var c=new SqlConnection(master.ConnectionString);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText=$"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";await cmd.ExecuteNonQueryAsync();Console.WriteLine("Temporary payment database removed.");}
}
Console.WriteLine("Payment checks passed. HTTP provider responses are fixtures; no live gateway was charged.");
static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
static async Task Reject<T>(Func<Task> action,string name) where T:Exception{try{await action();}catch(T){Check(true,name);return;}throw new Exception(name);}
sealed class FakeGateway:IFashionPaymentGateway {
    public int Calls;public IReadOnlyList<PaymentMethodView> Methods()=>[new("COD","COD",true,null),new("VNPay","VNPay",true,null),new("MoMo","MoMo",true,null)];
    public Task<string>CreateAsync(PaymentRequest p,string ip,CancellationToken ct){Calls++;return Task.FromResult("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?ref="+p.Id.ToString("N"));}
}
sealed class StubHandler:HttpMessageHandler {
    public string? LastBody;public bool BadUrl;
    protected override async Task<HttpResponseMessage>SendAsync(HttpRequestMessage request,CancellationToken ct){LastBody=await request.Content!.ReadAsStringAsync(ct);using var body=JsonDocument.Parse(LastBody);return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{resultCode=0,orderId=body.RootElement.GetProperty("orderId").GetString(),requestId=body.RootElement.GetProperty("requestId").GetString(),payUrl=BadUrl?"https://attacker.example.invalid/pay":"https://test-payment.momo.vn/v2/gateway/pay?test=fixture"}))};}
}
