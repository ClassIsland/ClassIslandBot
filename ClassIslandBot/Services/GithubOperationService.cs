using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using static Octokit.GraphQL.Variable;
using RestProductHeaderValue = Octokit.ProductHeaderValue;
using RestCredentials = Octokit.Credentials;
using RestAuthenticationType = Octokit.AuthenticationType;
using RestGitHubClient = Octokit.GitHubClient;

namespace ClassIslandBot.Services;

public class GithubOperationService(GitHubAuthService gitHubAuthService)
{
    private GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;

    public async Task<Connection> GetConnectionAsync()
    {
        return new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName),
            await GitHubAuthService.GetInstallationTokenAsync());
    }

    private async Task<RestGitHubClient> GetRestClientAsync()
    {
        return new RestGitHubClient(new RestProductHeaderValue(GitHubAuthService.GitHubAppName))
        {
            Credentials = new RestCredentials(
                await GitHubAuthService.GetInstallationTokenAsync(),
                RestAuthenticationType.Bearer)
        };
    }

    public async Task<IReadOnlyList<GitHubRepositoryOption>> GetRepositoriesAsync(string? keyword = null)
    {
        var client = await GetRestClientAsync();
        var response = await client.GitHubApps.Installation.GetAllRepositoriesForCurrent();
        var query = response.Repositories
            .Select(x => new GitHubRepositoryOption(
                x.NodeId,
                x.Name,
                x.FullName,
                x.Description,
                x.HtmlUrl))
            .OrderBy(x => x.FullName)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        return query.Take(50).ToList();
    }

    public async Task<IReadOnlyList<GitHubIssueOption>> GetIssuesAsync(
        string repoId,
        string? keyword = null,
        int take = 50)
    {
        var fetchSize = Math.Clamp(take, 1, 100);

        if (TryParseNumberFilter(keyword, out var number))
        {
            var issue = await GetIssueByNumberAsync(repoId, number);
            return issue == null ? [] : [issue];
        }

        var query = new Query()
            .Node(new ID(repoId))
            .Cast<Repository>()
            .Issues(first: fetchSize, states: new Arg<IEnumerable<IssueState>>([IssueState.Open, IssueState.Closed]))
            .Select(x => x.Nodes.Select(y => new GitHubIssueOption(
                y.Id.ToString(),
                y.Number,
                y.Title,
                y.State.ToString(),
                y.Url)).ToList())
            .Compile();

        var issues = await (await GetConnectionAsync()).Run(query);
        return FilterIssues(issues, keyword)
            .Take(fetchSize)
            .ToList();
    }

    public Task<IReadOnlyList<GitHubDiscussionOption>> GetVotingDiscussionsAsync(
        string? keyword = null,
        int take = 50) =>
        GetDiscussionsAsync(DiscussionService.VotingRepoId, keyword, take);

    private async Task<IReadOnlyList<GitHubDiscussionOption>> GetDiscussionsAsync(
        string repoId,
        string? keyword,
        int take)
    {
        var fetchSize = Math.Clamp(take, 1, 100);

        if (TryParseNumberFilter(keyword, out var number))
        {
            var discussion = await GetDiscussionByNumberAsync(repoId, number);
            return discussion == null ? [] : [discussion];
        }

        var query = new Query()
            .Node(new ID(repoId))
            .Cast<Repository>()
            .Discussions(first: fetchSize)
            .Select(x => x.Nodes.Select(y => new GitHubDiscussionOption(
                y.Id.ToString(),
                y.Number,
                y.Title,
                null,
                y.Url)).ToList())
            .Compile();

        var discussions = await (await GetConnectionAsync()).Run(query);
        return FilterDiscussions(discussions, keyword)
            .Take(fetchSize)
            .ToList();
    }

    public async Task<string> NormalizeIssueIdentifierAsync(string repoId, string issueIdentifier)
    {
        var trimmed = issueIdentifier.Trim();
        if (!TryParseNumberFilter(trimmed, out var number))
        {
            return trimmed;
        }

        return (await GetIssueNodeIdByNumberAsync(repoId, number)) ?? trimmed;
    }

    public async Task<string> NormalizeVotingDiscussionIdentifierAsync(string discussionIdentifier)
    {
        var trimmed = discussionIdentifier.Trim();
        if (!TryParseNumberFilter(trimmed, out var number))
        {
            return trimmed;
        }

        return (await GetDiscussionNodeIdByNumberAsync(DiscussionService.VotingRepoId, number)) ?? trimmed;
    }

    private async Task<string?> GetIssueNodeIdByNumberAsync(string repoId, int number) =>
        (await GetIssueByNumberAsync(repoId, number))?.Id;

    private async Task<string?> GetDiscussionNodeIdByNumberAsync(string repoId, int number) =>
        (await GetDiscussionByNumberAsync(repoId, number))?.Id;

    private async Task<GitHubIssueOption?> GetIssueByNumberAsync(string repoId, int number)
    {
        var query = new Query()
            .Node(new ID(repoId))
            .Cast<Repository>()
            .Issue(number)
            .Select(x => new GitHubIssueOption(
                x.Id.ToString(),
                x.Number,
                x.Title,
                x.State.ToString(),
                x.Url))
            .Compile();

        return await (await GetConnectionAsync()).Run(query);
    }

    private async Task<GitHubDiscussionOption?> GetDiscussionByNumberAsync(string repoId, int number)
    {
        var query = new Query()
            .Node(new ID(repoId))
            .Cast<Repository>()
            .Discussion(number)
            .Select(x => new GitHubDiscussionOption(
                x.Id.ToString(),
                x.Number,
                x.Title,
                null,
                x.Url))
            .Compile();

        return await (await GetConnectionAsync()).Run(query);
    }

    private static bool TryParseNumberFilter(string? keyword, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        var normalized = keyword.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        return int.TryParse(normalized, out number) && number > 0;
    }

    private static IEnumerable<GitHubIssueOption> FilterIssues(IEnumerable<GitHubIssueOption> issues, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return issues;
        }

        return issues.Where(x =>
            x.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            x.Number.ToString().Contains(keyword.TrimStart('#'), StringComparison.OrdinalIgnoreCase) ||
            x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<GitHubDiscussionOption> FilterDiscussions(
        IEnumerable<GitHubDiscussionOption> discussions,
        string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return discussions;
        }

        return discussions.Where(x =>
            x.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            x.Number.ToString().Contains(keyword.TrimStart('#'), StringComparison.OrdinalIgnoreCase) ||
            x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ID> AddCommentAsync(ID subjectId, string body)
    {
        var q = new Mutation()
            .AddComment(new Arg<AddCommentInput>(new AddCommentInput()
            {
                Body = body,
                SubjectId = subjectId
            }))
            .Select(x =>
                new
                {
                    x.ClientMutationId,
                    x.CommentEdge.Node.Id
                })
            .Compile();
        var connection = await GetConnectionAsync();
        return (await connection.Run(q)).Id;
    }

    public async Task LockLockableAsync(ID id)
    {
        var mutationLockLockable = new Mutation()
            .LockLockable(new Arg<LockLockableInput>(new LockLockableInput()
            {
                LockableId = id
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();
        var connection = await GetConnectionAsync();
        await connection.Run(mutationLockLockable);
    }

    public async Task CloseDiscussionAsync(ID id)
    {
        var mutation = new Mutation()
            .CloseDiscussion(new Arg<CloseDiscussionInput>(new CloseDiscussionInput()
            {
                DiscussionId = id,
                Reason = DiscussionCloseReason.Resolved,
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();
        var connection = await GetConnectionAsync();
        await connection.Run(mutation);
    }
    
    private async Task<ID> GetLabelIdByNameAsync(Connection connection, ID repoId, string name)
    {
        var qLabel = new Query()
            .Node(repoId)
            .Cast<Repository>()
            .Label(name)
            .Select(x => new
            {
                Id = x.Id,
            })
            .Compile();
        return (await connection.Run(qLabel)).Id;
    }

    public async Task RemoveLabelByNameAsync(ID labelableId, string labelName, ID repoId)
    {
        var connection = await GetConnectionAsync();
        var mutation = new Mutation()
            .RemoveLabelsFromLabelable(new Arg<RemoveLabelsFromLabelableInput>(new RemoveLabelsFromLabelableInput()
            {
                LabelIds =
                    [await GetLabelIdByNameAsync(connection, repoId, labelName)],
                LabelableId = labelableId
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();
        await connection.Run(mutation);
    }

    public async Task AddLabelByNameAsync(ID labelableId, string labelName, ID repoId)
    {
        var connection = await GetConnectionAsync();
        var mutationAddLabel = new Mutation()
            .AddLabelsToLabelable(new Arg<AddLabelsToLabelableInput>(new AddLabelsToLabelableInput()
            {
                LabelIds = [await GetLabelIdByNameAsync(connection, repoId, labelName)],
                LabelableId = labelableId
            }))
            .Select(x => new
            {
                Id = x.ClientMutationId
            })
            .Compile();
        await connection.Run(mutationAddLabel);
    }

    public async Task<bool> ValidateMemberAccessByNameAsync(IEnumerable<string> authorAssociation)
    {
        return false;
    }

    public async Task<Discussion> CreateDiscussionAsync(ID repoId, string title, string categoryName, string body)
    {
        throw new NotImplementedException();
    }
}

public record GitHubRepositoryOption(
    string Id,
    string Name,
    string FullName,
    string? Description,
    string Url);

public record GitHubIssueOption(
    string Id,
    int Number,
    string Title,
    string State,
    string Url);

public record GitHubDiscussionOption(
    string Id,
    int Number,
    string Title,
    string? State,
    string Url);
