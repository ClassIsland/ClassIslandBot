using ClassIslandBot.Abstractions;

namespace ClassIslandBot.Services;

public class IssueProcessBackgroundWorker(
    IBackgroundTaskQueue taskQueue,
    ILogger<IssueProcessBackgroundWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ProcessTaskQueueAsync(stoppingToken);
    }

    private async Task ProcessTaskQueueAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogTrace("Dequeued work item");
                var workItem = await taskQueue.DequeueAsync(stoppingToken);
                await workItem(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred executing task work item");
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            $"{nameof(IssueProcessBackgroundWorker)} is stopping.");

        await base.StopAsync(stoppingToken);
    }
}