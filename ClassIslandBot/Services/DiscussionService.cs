using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using Issue = Octokit.Webhooks.Models.Issue;

namespace ClassIslandBot.Services;

public class DiscussionService(GitHubAuthService gitHubAuthService, BotContext dbContext)
{
    public GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;
    public BotContext DbContext { get; } = dbContext;

    public async Task ConnectDiscussionAsync(long repoId, long issueId, Issue issue)
    {
        // TODO: 检查 Issue 是否重复

        var connection = new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName), 
            await GitHubAuthService.GetInstallationTokenAsync());
        
        
    }
}