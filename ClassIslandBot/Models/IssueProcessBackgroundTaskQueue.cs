using System.Threading.Channels;
using ClassIslandBot.Abstractions;

namespace ClassIslandBot.Models;

public class IssueProcessBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<(Func<CancellationToken, ValueTask>, TaskCompletionSource)> _queue;

    public IssueProcessBackgroundTaskQueue(int capacity)
    {
        BoundedChannelOptions options = new(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<(Func<CancellationToken, ValueTask>, TaskCompletionSource)>(options);
    }

    public async ValueTask<TaskCompletionSource> QueueBackgroundWorkItemAsync(
        Func<CancellationToken, ValueTask> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        var taskCompletionSource = new TaskCompletionSource();
        await _queue.Writer.WriteAsync((workItem, taskCompletionSource));
        return taskCompletionSource;
    }

    public async ValueTask QueueBackgroundWorkItemAndWaitAsync(Func<CancellationToken, ValueTask> workItem)
    {
        var tcs = await QueueBackgroundWorkItemAsync(workItem);
        await tcs.Task;
    }

    public async ValueTask<(Func<CancellationToken, ValueTask>, TaskCompletionSource)> DequeueAsync(
        CancellationToken cancellationToken)
    {
        var workItem =
            await _queue.Reader.ReadAsync(cancellationToken);

        return workItem;
    }
}