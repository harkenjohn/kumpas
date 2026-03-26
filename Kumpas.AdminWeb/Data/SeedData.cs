using Kumpas.AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace Kumpas.AdminWeb.Data;

public sealed class SeedData(IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment, ILogger<SeedData> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = configuration.GetValue<bool>("DevelopmentSeed:Enabled");
        if (!environment.IsDevelopment() || !enabled)
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KumpasDbContext>();

        try
        {
            if (!await db.SystemLogs.AnyAsync(cancellationToken))
            {
                db.SystemLogs.AddRange(
                    new SystemLog
                    {
                        Id = 900001,
                        UserId = null,
                        LogLevel = "INFO",
                        Module = "SeedData",
                        Message = "Sample log entry for development testing.",
                        ErrorStack = null,
                        Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
                    },
                    new SystemLog
                    {
                        Id = 900002,
                        UserId = null,
                        LogLevel = "INFO",
                        Module = "SeedData",
                        Message = "Second sample log entry for development testing.",
                        ErrorStack = null,
                        Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
                    });

                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Development seed skipped because the existing schema does not support test inserts.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
