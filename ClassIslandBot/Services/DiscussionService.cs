using System.Collections.Frozen;
using ClassIslandBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using Issue = Octokit.Webhooks.Models.Issue;

namespace ClassIslandBot.Services;

public class DiscussionService(GitHubAuthService gitHubAuthService, BotContext dbContext, ILogger<DiscussionService> logger)
{
    private const string FeatureSurveyDiscussionCategorySlug = "功能投票";

    private const string DiscussionTail = 
        """
        
        ***
        
        > [!note]
        > 这个 Discussion 复制自 Issue <{1}> 。点击左下角的“↑”来给这个功能进行投票，开发者会优先处理票数较高的帖子。
        >
        > 请在源 Issue 下进行讨论，不要在这个 Discussion 下面发表评论，**否则您的评论可能会在清除此 Discussion 时被清除**。
        """;

    private const string VotingRepoId = "R_kgDOM02_VQ";

    private static readonly FrozenDictionary<string, string> RepoMapping = (new Dictionary<string, string>()
            {
                { "R_kgDOJ5IdFQ", "ClassIsland" },  // ClassIsland/ClassIsland
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
                DiscussionId = x.Discussion.Id
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
        await DbContext.DiscussionAssociations.AddAsync(new DiscussionAssociation()
        {
            RepoId = repoId,
            IssueId = issueId,
            DiscussionId = result.DiscussionId.ToString()
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
            .DeleteDiscussion(new Arg<DeleteDiscussionInput>(new DeleteDiscussionInput()
            {
                Id = new ID(association.DiscussionId)
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
        association.IsTracking = false;
        await DbContext.SaveChangesAsync();
        Logger.LogTrace("Process #{} done", issue.Number);
    }
}