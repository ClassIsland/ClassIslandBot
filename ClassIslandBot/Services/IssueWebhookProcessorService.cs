using Octokit.GraphQL;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Models;

namespace ClassIslandBot.Services;

public class IssueWebhookProcessorService(GitHubAuthService gitHubAuthService, DiscussionService discussionService, ILogger<IssueWebhookProcessorService> logger) : WebhookEventProcessor
{
    public const string FeatureTagName = "新功能";
    public const string ImprovementTagName = "功能优化";
    public const string WipTagName = "处理中";
    public const string VotingTagName = "投票中";
    
    public GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;
    public DiscussionService DiscussionService { get; } = discussionService;
    public ILogger<IssueWebhookProcessorService> Logger { get; } = logger;

    protected override async Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        Logger.LogInformation("Received issue event: {} {}", issuesEvent.Issue.Id, issuesEvent.Action);
        // 判断是否属于功能请求类 Issue
        if (!issuesEvent.Issue.Labels.Any(x => x.Name is FeatureTagName or ImprovementTagName))
            return;
        
        
        switch (action)
        {
            case "labeled" when issuesEvent.Issue.State?.Value == IssueState.Open && !issuesEvent.Issue.Labels.Any(x => x.Name is VotingTagName or WipTagName):
                await DiscussionService.ConnectDiscussionAsync(issuesEvent.Repository?.NodeId ?? "", issuesEvent.Issue.NodeId, issuesEvent.Issue);
                break;
            
            // 当 Issue 进行中或者已完成时，移除对应的投票 Discussion
            case "labeled" when issuesEvent is IssuesLabeledEvent { Label.Name: WipTagName }:
            case "closed":
                await DiscussionService.DeleteDiscussionAsCompletedAsync(issuesEvent.Repository?.NodeId ?? "",
                    issuesEvent.Issue.NodeId, issuesEvent.Issue);
                break;
        }

    }

    protected override async Task ProcessPingWebhookAsync(WebhookHeaders headers, PingEvent pingEvent)
    {
        Logger.LogInformation("Ping!");
    }
    
}