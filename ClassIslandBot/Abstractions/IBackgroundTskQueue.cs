namespace ClassIslandBot.Abstractions;

public interface IBackgroundTaskQueue
{
    ValueTask<TaskCompletionSource> QueueBackgroundWorkItemAsync(
        Func<CancellationToken, ValueTask> workItem);
    
    ValueTask QueueBackgroundWorkItemAndWaitAsync(
        Func<CancellationToken, ValueTask> workItem);

    ValueTask<(Func<CancellationToken, ValueTask>, TaskCompletionSource)> DequeueAsync(
        CancellationToken cancellationToken);
    
}