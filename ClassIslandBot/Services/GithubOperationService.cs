using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using static Octokit.GraphQL.Variable;

namespace ClassIslandBot.Services;

public class GithubOperationService(GitHubAuthService gitHubAuthService)
{
    private GitHubAuthService GitHubAuthService { get; } = gitHubAuthService;

    public async Task<Connection> GetConnectionAsync()
    {
        return new Connection(new ProductHeaderValue(GitHubAuthService.GitHubAppName),
            await GitHubAuthService.GetInstallationTokenAsync());
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