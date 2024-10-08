using GitHubJwt;
using Octokit;

namespace ClassIslandBot.Services;

public class GitHubAuthService(ILogger<GitHubAuthService> logger)
{
    public const string GitHubAppName = "classisland-bot";
    
    public ILogger<GitHubAuthService> Logger { get; } = logger;
    
    public const int InstallationTokenExpireSeconds = 3600;
    public const int AppTokenExpireSeconds = 120;
    private const int AppId = 998668;
    private const string OrgName = "ClassIsland";

    private GitHubJwtFactory GitHubJwtFactory { get; } = new GitHubJwt.GitHubJwtFactory(
        new GitHubJwt.FilePrivateKeySource("./private-key.pem"),
        new GitHubJwt.GitHubJwtFactoryOptions
        {
            AppIntegrationId = AppId, // The GitHub App Id
            ExpirationSeconds = AppTokenExpireSeconds // 10 minutes is the maximum time allowed
        }
    );


    private DateTime LastRefreshInstallationToken { get; set; } = DateTime.MinValue;
    
    private DateTime LastRefreshAppToken { get; set; } = DateTime.MinValue;
    
    private string? InstallationTokenValue { get; set; }
    
    private string? AppTokenValue { get; set; }

    public async Task<string> GetInstallationTokenAsync()
    {
        if (DateTime.Now - LastRefreshInstallationToken <= TimeSpan.FromSeconds(InstallationTokenExpireSeconds) &&
            InstallationTokenValue != null) 
            return InstallationTokenValue;
        
        Logger.LogInformation("Refresh installation token because it is expired or null");

        if (DateTime.Now - LastRefreshAppToken > TimeSpan.FromSeconds(AppTokenExpireSeconds) || AppTokenValue == null)
        {
            Logger.LogInformation("Refresh app token because it is expired or null");

            AppTokenValue = GitHubJwtFactory.CreateEncodedJwtToken();
            LastRefreshAppToken = DateTime.Now;
            Logger.LogInformation("Refresh app token successfully");
        }
        var github = new GitHubClient(new ProductHeaderValue(GitHubAuthService.GitHubAppName))
        {
            Credentials = new Credentials(AppTokenValue, AuthenticationType.Bearer)
        };
        var installation = await github.GitHubApps.GetOrganizationInstallationForCurrent(OrgName);
        var installationId = installation.Id;
        InstallationTokenValue = (await github.GitHubApps.CreateInstallationToken(installationId)).Token;
        LastRefreshInstallationToken = DateTime.Now;
        Logger.LogInformation("Refresh installation token successfully");

        return InstallationTokenValue;
    }
}