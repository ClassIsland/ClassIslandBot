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

public class DiscussionService(GitHubAuthService gitHubAuthService, BotContext dbContext, ILogger<DiscussionService> logger)
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
    
    private async Task<ID> GetLabelIdByNameAsync(Connection connection, string repoId, string name)
    {
        var qLabel = new Query()
            .Node(new ID(repoId))
            .Cast<Repository>()
            .Label(name)
            .Select(x => new
            {
                Id = x.Id,
            })
            .Compile();
        return (await connection.Run(qLabel)).Id;
    }

    public async Task ConnectDiscussionAsync(string repoId, string issueId, Issue issue)
    {
        if (await DbContext.DiscussionAssociations.AnyAsync(x => x.IssueId == issueId))
        {
            Logger.LogInformation("Skipped adding issue #{} for duplicated", issue.Number);
            return;
        }

        if (!RepoMapping.TryGetValue(repoId, out var discussionCategory))
        {
            return;
        }
        
        var connection = new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName), 
            await GitHubAuthService.GetInstallationTokenAsync());

        var mutationCreateDiscussion = new Mutation()
            .CreateDiscussion(new Arg<CreateDiscussionInput>(new CreateDiscussionInput()
            {
                RepositoryId = new ID(VotingRepoId),
                CategoryId = await GetDiscussionCategoryIdBySlugAsync(connection, discussionCategory),
                Body = issue.Body + string.Format(DiscussionTail, $"#{issue.Number}", issue.HtmlUrl),
                Title = issue.Title,
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId,
                DiscussionId = x.Discussion.Id,
                Url = x.Discussion.Url
            })
            .Compile();
        var mutationAddLabel = new Mutation()
            .AddLabelsToLabelable(new Arg<AddLabelsToLabelableInput>(new AddLabelsToLabelableInput()
            {
                LabelIds = [await GetLabelIdByNameAsync(connection, repoId, IssueWebhookProcessorService.VotingTagName)],
                LabelableId = new ID(issueId)
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();


        var result =await connection.Run(mutationCreateDiscussion);
        await connection.Run(mutationAddLabel);
        var mutationAddRefComment = new Mutation()
            .AddComment(new Arg<AddCommentInput>(new AddCommentInput()
            {
                Body = string.Format(DiscussionReference, $"{result.Url}"),
                SubjectId = new ID(issueId)
            }))
            .Select(x =>
                new
                {
                    x.ClientMutationId,
                    x.CommentEdge.Node.Id
                })
            .Compile();
        var resultAddRefComment = await connection.Run(mutationAddRefComment);
        await DbContext.DiscussionAssociations.AddAsync(new DiscussionAssociation()
        {
            RepoId = repoId,
            IssueId = issueId,
            DiscussionId = result.DiscussionId.ToString(),
            RefCommentId = resultAddRefComment.Id.ToString()
        });
        await DbContext.SaveChangesAsync();
        Logger.LogTrace("Process #{} done", issue.Number);
    }

    public async Task DeleteDiscussionAsCompletedAsync(string repoId, string issueId, Issue issue)
    {
        var association = await DbContext.DiscussionAssociations.FirstOrDefaultAsync(x => x.IssueId == issueId && x.IsTracking);
        if (association == null)
        {
            Logger.LogInformation("Skipped removing discussion associated to issue #{} for not existed or not tracking", issue.Number);
            return;
        }
        if (!RepoMapping.TryGetValue(repoId, out var discussionCategory))
        {
            return;
        }
        
        var connection = new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName), 
            await GitHubAuthService.GetInstallationTokenAsync());
        
        
        var mutation = new Mutation()
            .CloseDiscussion(new Arg<CloseDiscussionInput>(new CloseDiscussionInput()
            {
                DiscussionId = new ID(association.DiscussionId),
                Reason = DiscussionCloseReason.Resolved,
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();
        var mutationLockDiscussion = new Mutation()
            .LockLockable(new Arg<LockLockableInput>(new LockLockableInput()
            {
                LockableId = new ID(association.DiscussionId)
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();
        var mutationRmLabel = new Mutation()
            .RemoveLabelsFromLabelable(new Arg<RemoveLabelsFromLabelableInput>(new RemoveLabelsFromLabelableInput()
            {
                LabelIds = [await GetLabelIdByNameAsync(connection, repoId, IssueWebhookProcessorService.VotingTagName)],
                LabelableId = new ID(issueId)
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();


        await connection.Run(mutationRmLabel);
        var result =await connection.Run(mutation);
        await connection.Run(mutationLockDiscussion);
        association.IsTracking = false;
        await DbContext.SaveChangesAsync();
        Logger.LogTrace("Process #{} done", issue.Number);
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
                        Issue = new Issue(){
                            NodeId = y.Id.ToString(),
                            Title = y.Title,
                            Body = y.Body,
                            HtmlUrl = y.Url,
                            Number = y.Number,
                        },
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
                    Logger.LogInformation("Processing untracked issue #{} in {}: {}", i.Issue.Number, repo, i.Issue.Title);
                    await ConnectDiscussionAsync(repo, i.Issue.NodeId.ToString(), i.Issue);
                }

            } while (vars["after"] != null);
        }
    }
}