namespace ClassIslandBot.Services.Authentication;

public static class AuthConstants
{
    public const string GitHubProviderName = "GitHub";
    public const string GitHubRegistrationId = "github";
    public const string ReturnUrlProperty = "returnUrl";
    public const string ProviderNameProperty = "provider_name";
    public const string BackchannelAccessToken = "backchannel_access_token";
    public const string GitHubUserIdClaim = "urn:github:user:id";
    public const string GitHubLoginClaim = "urn:github:login";
    public const string GitHubAvatarUrlClaim = "urn:github:avatar_url";
    public const string GitHubHtmlUrlClaim = "urn:github:html_url";
    public const string GitHubOrganizationClaim = "urn:github:organization";
}
