using System.Collections.Frozen;
using ClassIslandBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using Connection = Octokit.GraphQL.Connection;
using Issue = Octokit.Webhooks.Models.Issue;
using ProductHeaderValue = Octokit.GraphQL.ProductHeaderValue;
using Repository = Octokit.GraphQL.Model.Repository;
using static Octokit.GraphQL.Variable;
using Label = Octokit.Label;
using LockReason = Octokit.GraphQL.Model.LockReason;

namespace ClassIslandBot.Services;

public class DiscussionService(GitHubAuthService gitHubAuthService, BotContext dbContext, ILogger<DiscussionService> logger,
    GithubOperationService githubOperationService)
{
    private const string FeatureSurveyDiscussionCategorySlug = "功能投票";

    private const string DiscussionTail = 
        """
        
        ***
        
        > [!note]
        > 这个 Discussion 搬运自 Issue <{1}> 。点击左下角的“↑”来给这个功能进行投票，开发者会优先处理票数较高的帖子。
        >
        > 建议在源 Issue 下进行讨论。
        """;

    private const string DiscussionReference =
        "此功能请求已开始在投票贴 <{0}> 投票，欢迎前来为你想要的功能投票。";

    private const string VotingRepoId = "R_kgDOM02_VQ";

    private static readonly FrozenDictionary<string, string> RepoMapping = (new Dictionary<string, string>()
            {
#if !DEBUG
                { "R_kgDOJ5IdFQ", "ClassIsland" },  // ClassIsland/ClassIsland
#endif
                { "R_kgDOMyT8rg", "sandbox" },  // ClassIsland/sandbox
            }
        ).ToFrozenDictionary();
    
    private GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;
    private BotContext DbContext { get; } = dbContext;
    private ILogger<DiscussionService> Logger { get; } = logger;
    public GithubOperationService GithubOperationService { get; } = githubOperationService;

    private async Task<ID> GetDiscussionCategoryIdBySlugAsync(Connection connection, string slug)
    {
        var q = new Query()
            .Node(new ID(VotingRepoId))
            .Cast<Repository>()
            .DiscussionCategory(slug)
            .Select(x => new
            {
                Id = x.Id,
            })
            .Compile();
        
        var categoryId = (await connection.Run(q)).Id;
        return categoryId;
    }
    
    public async Task ConnectDiscussionAsync(string repoId, string issueId, Issue? issue=null)
    {
        if (await DbContext.DiscussionAssociations.AnyAsync(x => x.IssueId == issueId))
        {
            Logger.LogInformation("Skipped adding issue {} for duplicated", issueId);
            return;
        }

        if (!RepoMapping.TryGetValue(repoId, out var discussionCategory))
        {
            return;
        }
        
        var connection = new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName), 
            await GitHubAuthService.GetInstallationTokenAsync());

        var queryIssueInfo = new Query()
            .Node(new ID(issueId))
            .Cast<Octokit.GraphQL.Model.Issue>()
            .Select(x => new
            {
                x.Body,
                Number = (long)x.Number,
                x.Url,
                x.Title
            })
            .Compile();
        var issueInfo = issue == null
            ? await connection.Run(queryIssueInfo)
            : new
            {
                Body = issue.Body ?? "",
                Number = issue.Number,
                Url = issue.HtmlUrl,
                Title = issue.Title
            };

        var mutationCreateDiscussion = new Mutation()
            .CreateDiscussion(new Arg<CreateDiscussionInput>(new CreateDiscussionInput()
            {
                RepositoryId = new ID(VotingRepoId),
                CategoryId = await GetDiscussionCategoryIdBySlugAsync(connection, discussionCategory),
                Body = issueInfo.Body + string.Format(DiscussionTail, $"#{issueInfo.Number}", issueInfo.Url),
                Title = issueInfo.Title,
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId,
                DiscussionId = x.Discussion.Id,
                Url = x.Discussion.Url,
            })
            .Compile();
        
        var result =await connection.Run(mutationCreateDiscussion);
        await GithubOperationService.AddLabelByNameAsync(new ID(issueId), IssueWebhookProcessorService.VotingTagName,
            new ID(repoId));
        var commentId = await GithubOperationService.AddCommentAsync(new ID(issueId), string.Format(DiscussionReference, $"{result.Url}"));
        await DbContext.DiscussionAssociations.AddAsync(new DiscussionAssociation()
        {
            RepoId = repoId,
            IssueId = issueId,
            DiscussionId = result.DiscussionId.ToString(),
            RefCommentId = commentId.ToString()
        });
        await DbContext.SaveChangesAsync();
        Logger.LogTrace("Process #{} done", issueInfo.Number);
    }

    public async Task DeleteDiscussionAsCompletedAsync(string repoId, string issueId, Issue? issue=null)
    {
        var association = await DbContext.DiscussionAssociations.FirstOrDefaultAsync(x => x.IssueId == issueId && x.IsTracking);
        if (association == null)
        {
            Logger.LogInformation("Skipped removing discussion associated to issue {} for not existed or not tracking", issueId);
            return;
        }
        if (!RepoMapping.TryGetValue(repoId, out var discussionCategory))
        {
            return;
        }
        
        var connection = new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName), 
            await GitHubAuthService.GetInstallationTokenAsync());
        
        var queryIssueInfo = new Query()
            .Node(new ID(issueId))
            .Cast<Octokit.GraphQL.Model.Issue>()
            .Select(x => new
            {
                x.Body,
                Number = (long)x.Number,
                x.Url,
                x.Title
            })
            .Compile();
        var issueInfo = issue == null
            ? await connection.Run(queryIssueInfo)
            : new
            {
                Body = issue.Body ?? "",
                Number = issue.Number,
                Url = issue.HtmlUrl,
                Title = issue.Title
            };
        
        await GithubOperationService.RemoveLabelByNameAsync(new ID(issueId), IssueWebhookProcessorService.VotingTagName,
            new ID(repoId));
        await GithubOperationService.CloseDiscussionAsync(new ID(association.DiscussionId));
        await GithubOperationService.LockLockableAsync(new ID(association.DiscussionId));
        association.IsTracking = false;
        await DbContext.SaveChangesAsync();
        Logger.LogTrace("Process #{} done", issueInfo.Number);
    }

    public async Task SyncUnConnectedIssuesAsync()
    {
        var connection = new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName), 
            await GitHubAuthService.GetInstallationTokenAsync());

        foreach (var (repo, _) in RepoMapping)
        {
            var query = new Query()
                .Node(new ID(repo))
                .Cast<Repository>()
                .Issues(first: 100, states: new Arg<IEnumerable<IssueState>>([IssueState.Open]),
                    labels: new Arg<IEnumerable<string>>([IssueWebhookProcessorService.FeatureTagName, IssueWebhookProcessorService.ImprovementTagName]),
                    after: Var("after"))
                .Select(x => new
                {
                    x.PageInfo.EndCursor,
                    x.PageInfo.HasNextPage,
                    x.TotalCount,
                    Items = x.Nodes.Select(y => new
                    {
                        IssueId = y.Id.ToString(),
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
                foreach (var i in result.Items.Where(x => !x.Labels.Any(y =>
                             y.Name is IssueWebhookProcessorService.VotingTagName
                                 or IssueWebhookProcessorService.ReviewingTagName
                                 or IssueWebhookProcessorService.WipTagName)))
                {
                    Logger.LogInformation("Processing untracked issue {} in {}", i.IssueId, repo);
                    await ConnectDiscussionAsync(repo, i.IssueId);
                }

            } while (vars["after"] != null);
        }
        Logger.LogInformation("Synced untracked issues");
    }
}