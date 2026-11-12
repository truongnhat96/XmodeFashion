using UseCase.Fashion;

namespace Infrastructure.Fashion.Payments;

public sealed class PaymentExpirationWorker(IServiceScopeFactory scopes, ILogger<PaymentExpirationWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IFashionPayments>().ExpireAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { log.LogWarning("Unable to expire pending payments; retrying on next cycle."); }
        }
    }
}
