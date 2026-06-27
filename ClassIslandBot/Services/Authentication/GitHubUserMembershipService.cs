using Octokit;

namespace ClassIslandBot.Services.Authentication;

public sealed class GitHubUserMembershipService(
    IConfiguration configuration,
    ILogger<GitHubUserMembershipService> logger,
    GitHubAuthService gitHubAuthService)
{
    public string Organization =>
        configuration["Authentication:GitHub:Organization"] ?? "ClassIsland";

    public async Task<GitHubAuthenticatedUser?> ValidateOrganizationMemberAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = CreateClient(accessToken);
        var user = await GetCurrentUserAsync(client);
        if (user == null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isMember = await IsOrganizationMemberAsync(client, user.Login, cancellationToken);
        if (!isMember)
        {
            logger.LogWarning("GitHub user {Login} is not an active member of {Organization}.",
                user.Login,
                Organization);
            return null;
        }

        return new GitHubAuthenticatedUser(
            user.Id,
            user.Login,
            user.Name,
            user.AvatarUrl,
            user.HtmlUrl);
    }

    private async Task<User?> GetCurrentUserAsync(GitHubClient client)
    {
        try
        {
            return await client.User.Current();
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "Failed to load GitHub user profile. StatusCode: {StatusCode}.",
                ex.StatusCode);
            return null;
        }
    }

    private async Task<bool> IsOrganizationMemberAsync(
        GitHubClient userClient,
        string login,
        CancellationToken cancellationToken)
    {
        var userTokenResult = await TryCheckMemberAsync(userClient, Organization, login, "GitHub OAuth token");
        if (userTokenResult.HasValue)
        {
            return userTokenResult.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var installationToken = await gitHubAuthService.GetInstallationTokenAsync();
            var installationClient = CreateClient(installationToken);
            var installationResult = await TryCheckMemberAsync(
                installationClient,
                Organization,
                login,
                "GitHub App installation token");
            if (installationResult.HasValue)
            {
                return installationResult.Value;
            }
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex,
                "Failed to prepare GitHub App installation token for organization membership check. Organization: {Organization}, StatusCode: {StatusCode}.",
                Organization,
                ex.StatusCode);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var publicMemberResult = await TryCheckPublicMemberAsync(userClient, Organization, login);
        return publicMemberResult ?? false;
    }

    private async Task<bool?> TryCheckMemberAsync(
        GitHubClient client,
        string organization,
        string login,
        string credentialName)
    {
        try
        {
            return await client.Organization.Member.CheckMember(organization, login);
        }
        catch (NotFoundException)
        {
            return false;
        }
        catch (ForbiddenException ex)
        {
            logger.LogWarning(ex,
                "GitHub organization membership check was forbidden with {CredentialName}. Organization: {Organization}, Login: {Login}, StatusCode: {StatusCode}.",
                credentialName,
                organization,
                login,
                ex.StatusCode);
            return null;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex,
                "GitHub organization membership check failed with {CredentialName}. Organization: {Organization}, Login: {Login}, StatusCode: {StatusCode}.",
                credentialName,
                organization,
                login,
                ex.StatusCode);
            return null;
        }
    }

    private async Task<bool?> TryCheckPublicMemberAsync(
        GitHubClient client,
        string organization,
        string login)
    {
        try
        {
            return await client.Organization.Member.CheckMemberPublic(organization, login);
        }
        catch (NotFoundException)
        {
            return false;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex,
                "GitHub public organization membership fallback failed. Organization: {Organization}, Login: {Login}, StatusCode: {StatusCode}.",
                organization,
                login,
                ex.StatusCode);
            return null;
        }
    }

    private static GitHubClient CreateClient(string token) =>
        new(new ProductHeaderValue(GitHubAuthService.GitHubAppName))
        {
            Credentials = new Credentials(token, AuthenticationType.Bearer)
        };
}

public sealed record GitHubAuthenticatedUser(
    long Id,
    string Login,
    string? Name,
    string? AvatarUrl,
    string? HtmlUrl);
