using System.Text.RegularExpressions;
using ClassIslandBot.Services.Webhooks;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Events.Release;
using static Octokit.GraphQL.Variable;


namespace ClassIslandBot.Services;

public partial class ReleaseTrackingService(GithubOperationService githubOperationService, 
    ILogger<ReleaseTrackingService> logger)
{
    public GithubOperationService GithubOperationService { get; } = githubOperationService;
    public ILogger<ReleaseTrackingService> Logger { get; } = logger;
    public const string WaitingForReleaseLabelName = "待发布"; 
    
    private const string ReleaseNotificationStableTemplate = 
        "包含此{0}的版本已在稳定通道[{1}]({2})发布，请及时更新以获取包含此{0}的版本。";
    
    private const string ReleaseNotificationPreviewTemplate = 
        "包含此{0}的版本已在测试通道[{1}]({2})发布，请及时更新以获取包含此{0}的版本。要在应用内升级到此版本，您可能需要在【应用设置】->【更新】->【更新设置】中将更新通道切换到对应的测试通道。";

    private const string NoReleaseTrackingTag = "{!no_release_tracking}";
    
    [GeneratedRegex("#(\\d+)")]
    private static partial Regex IssueNumberMatchingRegex();

    public async Task ProcessClosedIssueAsync(IssuesClosedEvent issuesClosedEvent)
    {
        Logger.LogInformation("Processing prepare for release issue #{} in {}", issuesClosedEvent.Issue.Number, issuesClosedEvent.Repository?.NodeId);
        if (issuesClosedEvent.Issue.StateReason != "completed")
        {
            Logger.LogInformation("Skipped for close reason is not 'completed'");
            return;
        }

        await GithubOperationService.AddLabelByNameAsync(new ID(issuesClosedEvent.Issue.NodeId),
            WaitingForReleaseLabelName,
            new ID(issuesClosedEvent.Repository?.NodeId));
    }
    
    public async Task ProcessReleaseNoteAsync(ReleasePublishedEvent releasePublishedEvent)
    {
        Logger.LogInformation("Processing release note {} in {}", releasePublishedEvent.Release.Name, releasePublishedEvent.Repository?.NodeId);
        var re = IssueNumberMatchingRegex();
        var numbers = re.Matches(releasePublishedEvent.Release.Body)
            .Where(x => x.Groups.Count >= 2)
            .Select(x => x.Groups[1].ToString())
            .ToList();
        var connection = await GithubOperationService.GetConnectionAsync();
        var query = new Query()
            .Node(new ID(releasePublishedEvent.Repository?.NodeId))
            .Cast<Repository>()
            .Issues(first: 100,
                labels: new Arg<IEnumerable<string>>([WaitingForReleaseLabelName]),
                after: Var("after"))
            .Select(x => new
            {
                x.PageInfo.EndCursor,
                x.PageInfo.HasNextPage,
                x.TotalCount,
                Items = x.Nodes.Select(y => new
                {
                    IssueId = y.Id,
                    y.Number,
                    Labels = y.Labels(100, null, null, null, null).Nodes.Select(label => new
                    {
                        label.Name
                    }).ToList()
                }).ToList(),
                
            })
            .Compile();
        var vars = new Dictionary<string, object?>
        {
            { "after", null },
        };
        do
        {
            var result = await connection.Run(query);
            vars["after"] = result.HasNextPage ? result.EndCursor : null;
            foreach (var i in result.Items.Where(x => numbers.Contains(x.Number.ToString())))
            {
                Logger.LogInformation("Processing waiting-for-release issue {} in {}", i.IssueId, releasePublishedEvent.Repository?.NodeId);
                string issueKind;
                if (i.Labels.Exists(x => x.Name is IssueWebhookProcessorService.FeatureTagName or IssueWebhookProcessorService.ImprovementTagName))
                {
                    issueKind = "功能请求";
                } else if (i.Labels.Exists(x => x.Name is "Bug"))
                {
                    issueKind = " Bug 的修复";
                }
                else
                {
                    issueKind = "提议";
                }

                await GithubOperationService.AddCommentAsync(i.IssueId,
                    string.Format(
                        releasePublishedEvent.Release.Prerelease
                            ? ReleaseNotificationPreviewTemplate
                            : ReleaseNotificationStableTemplate, 
                        issueKind, releasePublishedEvent.Release.Name,
                        releasePublishedEvent.Release.HtmlUrl));
                await GithubOperationService.RemoveLabelByNameAsync(i.IssueId, WaitingForReleaseLabelName,
                    new ID(releasePublishedEvent.Repository?.NodeId));
            }

        } while (vars["after"] != null);
    }
}