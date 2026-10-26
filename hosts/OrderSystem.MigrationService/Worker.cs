using Microsoft.EntityFrameworkCore;
using OrderSystem.Adapters.Postgres;

namespace OrderSystem.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        logger.LogInformation("Applying pending EF Core migrations for {Database}", dbContext.Database.GetDbConnection().Database);

        await dbContext.Database.MigrateAsync(stoppingToken);

        logger.LogInformation("Migrations applied successfully");

        hostLifetime.StopApplication();
    }
}
