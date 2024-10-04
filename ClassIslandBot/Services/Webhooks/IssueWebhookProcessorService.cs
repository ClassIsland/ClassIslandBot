using ClassIslandBot.Abstractions;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.IssueComment;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Events.Release;
using Octokit.Webhooks.Models;

namespace ClassIslandBot.Services.Webhooks;

public class IssueWebhookProcessorService(GitHubAuthService gitHubAuthService, 
    DiscussionService discussionService, 
    IServiceScopeFactory serviceScopeFactory,
    ILogger<IssueWebhookProcessorService> logger,
    IBackgroundTaskQueue taskQueue) : WebhookEventProcessor
{
    public const string FeatureTagName = "新功能";
    public const string ImprovementTagName = "功能优化";
    public const string WipTagName = "处理中";
    public const string VotingTagName = "投票中";
    public const string ReviewingTagName = "待查看";
    
    public GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;
    public DiscussionService DiscussionService { get; } = discussionService;
    public ILogger<IssueWebhookProcessorService> Logger { get; } = logger;
    public IBackgroundTaskQueue TaskQueue { get; } = taskQueue;

    protected override async Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        Logger.LogInformation("Received issue event: {} {}", issuesEvent.Issue.Id, issuesEvent.Action);
        
        await TaskQueue.QueueBackgroundWorkItemAsync(async (_) =>
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var discussionService = scope.ServiceProvider.GetRequiredService<DiscussionService>();
            await ProcessFeatureIssue(issuesEvent, action, discussionService);
            var releaseTrackingService = scope.ServiceProvider.GetRequiredService<ReleaseTrackingService>();
            await ProcessReleaseActions(issuesEvent, action, releaseTrackingService);
        });
    }

    private async Task ProcessReleaseActions(IssuesEvent issuesEvent, IssuesAction action, ReleaseTrackingService releaseTrackingService)
    {
        if (action != "closed")
        {
            return;
        }

        if (issuesEvent is IssuesClosedEvent issuesClosedEvent)
        {
            await releaseTrackingService.ProcessClosedIssueAsync(issuesClosedEvent);
        }
    }

    protected override async Task ProcessIssueCommentWebhookAsync(WebhookHeaders headers, IssueCommentEvent issueCommentEvent,
        IssueCommentAction action)
    {
        Logger.LogInformation("Received issue comment event: {} {}", issueCommentEvent.Issue.Id, issueCommentEvent.Action);
        if (issueCommentEvent.Action != "created")
        {
            return;
        }
        await TaskQueue.QueueBackgroundWorkItemAsync(async (_) =>
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IssueCommandProcessService>();
            await service.ProcessCommandAsync(issueCommentEvent.Issue.NodeId, issueCommentEvent.Comment.NodeId, issueCommentEvent);
        });
    }

    private async Task ProcessFeatureIssue(IssuesEvent issuesEvent, IssuesAction action, DiscussionService discussionService)
    {
        if (!issuesEvent.Issue.Labels.Any(x => x.Name is FeatureTagName or ImprovementTagName))
            return;
        var validForAddDiscussion = issuesEvent.Issue.State?.Value == IssueState.Open && !issuesEvent.Issue.Labels.Any(x => x.Name is VotingTagName or WipTagName or ReviewingTagName);
        switch (action)
        {
            case "unlabeled" when validForAddDiscussion:
            case "labeled" when validForAddDiscussion:
                await discussionService.ConnectDiscussionAsync(issuesEvent.Repository?.NodeId ?? "",
                    issuesEvent.Issue.NodeId, issuesEvent.Issue);
                break;
            
            // 当 Issue 进行中或者已完成时，移除对应的投票 Discussion
            case "labeled" when issuesEvent is IssuesLabeledEvent { Label.Name: WipTagName }:
            case "closed":
                await discussionService.DeleteDiscussionAsCompletedAsync(issuesEvent.Repository?.NodeId ?? "",
                    issuesEvent.Issue.NodeId, issuesEvent.Issue);
                
                break;
        }
    }

    protected override async Task ProcessPingWebhookAsync(WebhookHeaders headers, PingEvent pingEvent)
    {
        Logger.LogInformation("Ping!");
    }
    
    protected override async Task ProcessReleaseWebhookAsync(WebhookHeaders headers, ReleaseEvent releaseEvent, ReleaseAction action)
    {
        Logger.LogInformation("Received release event: {} {}", releaseEvent.Release.TagName, releaseEvent.Action);

        await TaskQueue.QueueBackgroundWorkItemAsync(async (_) =>
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            if (action != "published" || releaseEvent is not ReleasePublishedEvent publishedEvent)
            {
                return;
            }
            var service = scope.ServiceProvider.GetRequiredService<ReleaseTrackingService>();
            await service.ProcessReleaseNoteAsync(publishedEvent);
        });
    }
    
}