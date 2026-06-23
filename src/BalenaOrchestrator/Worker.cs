namespace BalenaOrchestrator;

public class Worker(ILogger<Worker> logger, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var balenaApiBase = config["Balena:ApiBase"] ?? "https://api.balena-cloud.com";

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Reconciling balena assignments at {time} using {apiBase}", DateTimeOffset.Now, balenaApiBase);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
