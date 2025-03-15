using System.Text.RegularExpressions;
using ClassIslandBot.Helpers;
using Octokit.GraphQL;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;
using Octokit.Webhooks.Models.CommitCommentEvent;

namespace ClassIslandBot.Services;


public partial class IssueCommandProcessService(GithubOperationService githubOperationService, 
    ILogger<IssueCommandProcessService> logger,
    DiscussionService discussionService,
    IssueLabelService issueLabelService)
{
    public GithubOperationService GithubOperationService { get; } = githubOperationService;
    public ILogger<IssueCommandProcessService> Logger { get; } = logger;
    public DiscussionService DiscussionService { get; } = discussionService;
    public IssueLabelService IssueLabelService { get; } = issueLabelService;

    private static readonly string[] AuthorizedLevels = ["owner", "member"];
    
    private const string UnAuthorizedCommentTemplate = "你没有进行此操作的权限。 ";
    private const string TrackedIssueVotingCommentTemplate = "已开始对此 Issue 进行投票。 ";
    private const string UnTrackedIssueVotingCommentTemplate = "已停止对此 Issue 进行投票。 ";
    private const string ErrorCommentTemplate = "无法进行此操作，详见应用日志。";
    
    private const string PingCommentTemplate = 
        """
        
        <img alt="流萤比心" src="https://github.com/user-attachments/assets/cc7902f8-baa7-4a70-9fec-4792f401cdd4" height="80px"/>
        
        Pong!
        """;
    
    [GeneratedRegex("@classisland-bot /(.+)")]
    private static partial Regex CommandMatchingRegex();
    
    public async Task ProcessCommandAsync(string issueId, string commentId, IssueCommentEvent issueCommentEvent)
    {
        var re = CommandMatchingRegex();
        var match = re.Match(issueCommentEvent.Comment.Body);
        if (!match.Success || match.Groups.Count < 2)
        {
            return;
        }

        var commandFull = match.Groups[1];
        Logger.LogInformation("Process command: {} {} {}", issueCommentEvent.Comment.User.Login, 
            issueCommentEvent.Comment.AuthorAssociation.StringValue.ToLower(),
            commandFull);
        
        // authorize
        if (issueCommentEvent.Repository?.Private != true &&
            !AuthorizedLevels.Contains(issueCommentEvent.Comment.AuthorAssociation.StringValue.ToLower()))
        {
            await Comment(UnAuthorizedCommentTemplate);
            return;
        }

        var commands = commandFull.ToString().Split(' ');
        try
        {
            switch (commands[0])
            {
                case "ping":
                    await Comment(PingCommentTemplate);
                    break;
                case "track_voting":
                    await DiscussionService.ConnectDiscussionAsync(issueCommentEvent.Repository?.NodeId ?? ""
                        , issueCommentEvent.Issue.NodeId, force:true);
                    await Comment(TrackedIssueVotingCommentTemplate);
                    break;
                case "untrack_voting":
                    await DiscussionService.DeleteDiscussionAsCompletedAsync(issueCommentEvent.Repository?.NodeId ?? ""
                        , issueCommentEvent.Issue.NodeId);
                    await Comment(UnTrackedIssueVotingCommentTemplate);
                    break;
                case "label":
                    await IssueLabelService.LabelIssueAsync(
                        IssueBodyHelpers.ExtractIssue(issueCommentEvent.Issue.Body ?? "",
                            issueCommentEvent.Issue.Labels.Select(x => x.Name)), new ID(issueCommentEvent.Issue.NodeId),
                        new ID(issueCommentEvent.Repository?.NodeId));
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unable to execute command {}", commandFull);
            await Comment(ErrorCommentTemplate);
        }

        return;

        string WrapComment(string comment) => $"@{issueCommentEvent.Comment.User.Login} {comment}";
        async Task Comment(string comment) => await GithubOperationService.AddCommentAsync(new ID(issueId),
            WrapComment(comment));
    }

    
}