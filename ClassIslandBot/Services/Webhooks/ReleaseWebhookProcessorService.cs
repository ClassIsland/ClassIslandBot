using ClassIslandBot.Abstractions;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Release;

namespace ClassIslandBot.Services.Webhooks;

public class ReleaseWebhookProcessorService(GitHubAuthService gitHubAuthService, 
    DiscussionService discussionService, 
    IServiceScopeFactory serviceScopeFactory,
    ILogger<IssueWebhookProcessorService> logger,
    IBackgroundTaskQueue taskQueue) : WebhookEventProcessor
{
    
    public ILogger<IssueWebhookProcessorService> Logger { get; } = logger;
    public IBackgroundTaskQueue TaskQueue { get; } = taskQueue;

    
}