using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Infrastructure.Data.Fashion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var root = Directory.GetCurrentDirectory();
var database = $"XmodeFashion_E2E_{Guid.NewGuid():N}";
var master = new SqlConnectionStringBuilder { DataSource = @".\SQLEXPRESS", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = database };
var created = false;
Process? host = null;
var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
try
{
    await using (var c = new SqlConnection(master.ConnectionString))
    {
        await c.OpenAsync(); await using var cmd = c.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{database}]"; await cmd.ExecuteNonQueryAsync(); created = true;
    }
    var init = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    init.ArgumentList.Add(Path.Combine(root, "tools", "fashion-init", "bin", "Debug", "net9.0", "FashionInit.dll")); init.ArgumentList.Add("--test");
    init.Environment["ConnectionStrings__FashionConnection"] = test.ConnectionString;
    init.Environment["XMODE_ADMIN_USER"] = "e2e_admin"; init.Environment["XMODE_ADMIN_EMAIL"] = "admin@example.invalid";
    init.Environment["XMODE_ADMIN_PASSWORD"] = "E2e-Only-Password-127";
    async Task<int> RunInitializer(bool schemaOnly)
    {
        if (schemaOnly) init.ArgumentList.Add("--schema-only");
        using var initializer = Process.Start(init) ?? throw new Exception("Could not start initializer.");
        var initOut = initializer.StandardOutput.ReadToEndAsync(); var initError = initializer.StandardError.ReadToEndAsync();
        await initializer.WaitForExitAsync(); Console.Write(await initOut); Console.Error.Write(await initError);
        if (schemaOnly) init.ArgumentList.Remove("--schema-only");
        return initializer.ExitCode;
    }
    if (await RunInitializer(true) != 0 || await RunInitializer(false) != 0 || await RunInitializer(true) != 0)
        throw new Exception("Database initialization failed.");
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "Infrastructure"), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    start.ArgumentList.Add(Environment.GetEnvironmentVariable("XMODE_E2E_APP_DLL")
        ?? Path.Combine(root, "Infrastructure", "bin", "Debug", "net9.0", "Infrastructure.dll"));
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    start.Environment["Logging__LogLevel__Default"] = "Warning";
    // Browser contract checks never consume a real Groq key or make billable LLM calls.
    start.Environment["Groq__ApiKey"] = " ";
    // Fixture-only VNPay signing; no real merchant keys and no network checkout.
    start.Environment["Vnpay__Enabled"] = "true";
    start.Environment["Vnpay__TmnCode"] = "E2ECODE";
    start.Environment["Vnpay__HashSecret"] = "e2e-vnpay-secret";
    start.Environment["Vnpay__BaseUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    start.Environment["Payments__PublicBaseUrl"] = "https://checkout.example.invalid";
    start.Environment["MomoAPI__Enabled"] = "true";
    start.Environment["MomoAPI__PartnerCode"] = "E2EMOMO";
    start.Environment["MomoAPI__AccessKey"] = "e2e-access";
    start.Environment["MomoAPI__SecretKey"] = "e2e-momo-secret";
    start.Environment["MomoAPI__Endpoint"] = "https://test-payment.momo.vn/v2/gateway/api/create";
    start.Environment["ConnectionStrings__FashionConnection"] = test.ConnectionString;
    host = Process.Start(start) ?? throw new Exception("Could not start web application.");
    host.OutputDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
    host.ErrorDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
    host.BeginOutputReadLine(); host.BeginErrorReadLine();
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    var url = $"http://127.0.0.1:{port}";
    var ready = false;
    for (var i = 0; i < 60 && !host.HasExited; i++)
    {
        try { if ((await http.GetAsync(url)).IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { } catch (TaskCanceledException) { }
        await Task.Delay(500);
    }
    if (!ready) throw new Exception("Web application did not start.");
    var node = new ProcessStartInfo("node") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    node.ArgumentList.Add(Path.Combine(root, "tools", "xmode-ui", "fashion-e2e.cjs"));
    node.Environment["XMODE_TEST_BASE"] = url;
    using var browser = Process.Start(node) ?? throw new Exception("Could not start browser test.");
    var browserOut = browser.StandardOutput.ReadToEndAsync();
    var browserError = browser.StandardError.ReadToEndAsync();
    await browser.WaitForExitAsync();
    Console.Write(await browserOut);
    Console.Error.Write(await browserError);
    if (browser.ExitCode != 0) throw new Exception($"Browser checks failed ({browser.ExitCode}).");
    Console.WriteLine("Fashion browser integration checks passed.");
}
catch
{
    foreach (var line in output.TakeLast(40)) Console.Error.WriteLine(line);
    throw;
}
finally
{
    if (host is { HasExited: false }) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
    host?.Dispose(); SqlConnection.ClearAllPools();
    if (created)
    {
        await using var c = new SqlConnection(master.ConnectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand();
        cmd.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
        await cmd.ExecuteNonQueryAsync(); Console.WriteLine("Temporary browser test database removed.");
    }
}
