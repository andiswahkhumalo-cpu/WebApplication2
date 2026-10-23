namespace WebApplication2.Portal;

public interface IAlertSender
{
    Task SendAsync(Alert alert, CancellationToken cancellationToken);
}

public sealed partial class LoggingAlertSender(ILogger<LoggingAlertSender> logger) : IAlertSender
{
    public Task SendAsync(Alert alert, CancellationToken cancellationToken)
    {
        logger.LogInformation("Portal alert to {Recipient}: {Subject} - {Body}", alert.Recipient, alert.Subject, alert.Body);
        return Task.CompletedTask;
    }
}

public sealed class ReminderWorker(IServiceScopeFactory scopes, ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var portal = scope.ServiceProvider.GetRequiredService<IPortalService>();
                var sender = scope.ServiceProvider.GetRequiredService<IAlertSender>();
                foreach (var alert in portal.FindDueAlerts(DateOnly.FromDateTime(DateTime.UtcNow)))
                {
                    try
                    {
                        await sender.SendAsync(alert, stoppingToken);
                        var delivery = portal.GetNotificationDeliveries().LastOrDefault(n => n.EntityId == alert.EntityId && n.Status == NotificationStatus.Queued);
                        if (delivery is not null) portal.MarkNotification(delivery, NotificationStatus.Delivered);
                    }
                    catch (Exception ex)
                    {
                        var delivery = portal.GetNotificationDeliveries().LastOrDefault(n => n.EntityId == alert.EntityId && n.Status == NotificationStatus.Queued);
                        if (delivery is not null) portal.MarkNotification(delivery, NotificationStatus.Failed, ex.Message);
                        throw;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Portal reminder run failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
