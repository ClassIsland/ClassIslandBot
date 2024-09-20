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
    
    private GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;
    public BotContext DbContext { get; } = dbContext;
    public ILogger<DiscussionService> Logger { get; } = logger;

    public async Task ConnectDiscussionAsync(string repoId, string issueId, Issue issue)
    {
        if (await DbContext.DiscussionAssociations.AnyAsync(x => x.IssueId == issueId))
        {
            Logger.LogInformation("Skipped adding issue #{} for duplicated", issue.Number);
            return;
        }
        
        var connection = new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName), 
            await GitHubAuthService.GetInstallationTokenAsync());

        var q = new Query()
            .Node(new ID(repoId))
            .Cast<Repository>()
            .DiscussionCategory(FeatureSurveyDiscussionCategorySlug)
            .Select(x => new
            {
                Id = x.Id,
            })
            .Compile();
        var categoryId = (await connection.Run(q)).Id;

        var mutation = new Mutation()
            .CreateDiscussion(new Arg<CreateDiscussionInput>(new CreateDiscussionInput()
            {
                RepositoryId = new ID(repoId),
                CategoryId = categoryId,
                Body = issue.Body,
                Title = issue.Title,
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId,
                DiscussionId = x.Discussion.Id
            })
            .Compile();


        var result =await connection.Run(mutation);
        await DbContext.DiscussionAssociations.AddAsync(new DiscussionAssociation()
        {
            RepoId = repoId,
            IssueId = issueId,
            DiscussionId = result.DiscussionId.ToString()
        });
        await DbContext.SaveChangesAsync();
        Logger.LogTrace("Process #{} done", issue.Number);
    }
}