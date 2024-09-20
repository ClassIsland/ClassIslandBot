using Octokit.GraphQL;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Issues;

namespace ClassIslandBot.Services;

public class IssueWebhookProcessorService(GitHubAuthService gitHubAuthService, DiscussionService discussionService, ILogger<IssueWebhookProcessorService> logger) : WebhookEventProcessor
{
    private const string FeatureTagName = "新功能";
    private const string ImprovementTagName = "功能优化";
    private const string WipTagName = "处理中";
    
    public GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;
    public DiscussionService DiscussionService { get; } = discussionService;
    public ILogger<IssueWebhookProcessorService> Logger { get; } = logger;

    protected override async Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        Logger.LogInformation("Received issue event: {} {}", issuesEvent.Issue.Id, issuesEvent.Action);
        // 判断是否属于功能请求类 Issue
        if (!issuesEvent.Issue.Labels.Any(x => x.Name is FeatureTagName or ImprovementTagName))
            return;
        

        await DiscussionService.ConnectDiscussionAsync(issuesEvent.Repository?.Id ?? -1, issuesEvent.Issue.Id, issuesEvent.Issue);

        return;
    }

    protected override async Task ProcessPingWebhookAsync(WebhookHeaders headers, PingEvent pingEvent)
    {
        Logger.LogInformation("Ping!");
    }
}