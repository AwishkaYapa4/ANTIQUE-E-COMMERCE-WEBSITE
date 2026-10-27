using AntiqueStore.Application.Inventory;
using AntiqueStore.Application.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntiqueStore.Infrastructure.BackgroundJobs;

public sealed class MaintenanceOptions
{
    public const string Section = "Maintenance";
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Periodically expires checkout reservations and auto-cancels overdue unpaid bank transfers.
/// Safe to run on several instances at once: every state change is a conditional update.
/// </summary>
public sealed class MaintenanceWorker(IServiceScopeFactory scopes, IOptions<MaintenanceOptions> options, ILogger<MaintenanceWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var interval = TimeSpan.FromSeconds(Math.Max(10, options.Value.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<InventoryService>().ExpireReservationsAsync(200, stoppingToken);
                var cancelled = await scope.ServiceProvider.GetRequiredService<OrderMaintenanceService>().CancelOverdueBankTransfersAsync(50, stoppingToken);
                if (expired > 0 || cancelled > 0)
                    logger.LogInformation("Maintenance: expired {Expired} reservations, auto-cancelled {Cancelled} orders", expired, cancelled);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Maintenance run failed; will retry on next tick");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
