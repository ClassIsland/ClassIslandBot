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
            TaskCompletionSource? completionSource = null;
            try
            {
                logger.LogTrace("Dequeued work item");
                var (workItem, completionSource1) = await taskQueue.DequeueAsync(stoppingToken);
                completionSource = completionSource1;
                await workItem(stoppingToken);
                completionSource.SetResult();
            }
            catch (OperationCanceledException)
            {
                completionSource?.SetCanceled(stoppingToken);
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                completionSource?.SetException(ex);
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