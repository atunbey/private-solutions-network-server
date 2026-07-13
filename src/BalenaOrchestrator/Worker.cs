namespace BalenaOrchestrator;

public class Worker(ILogger<Worker> logger, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var balenaApiBase = config["Balena:ApiBase"]
            ?? Environment.GetEnvironmentVariable("BALENA_API_BASE")
            ?? string.Empty;

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Reconciling openBalena assignments at {time} using {apiBase}", DateTimeOffset.Now, string.IsNullOrWhiteSpace(balenaApiBase) ? "<unset>" : balenaApiBase);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
