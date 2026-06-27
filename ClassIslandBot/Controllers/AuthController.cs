using System.Globalization;
using System.Security.Claims;
using ClassIslandBot.Services.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;

namespace ClassIslandBot.Controllers;

[ApiController]
public class AuthController(
    IConfiguration configuration,
    GitHubUserMembershipService gitHubUserMembershipService,
    ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/api/v1/auth/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);
        var callbackPath = GetGitHubCallbackPath();
        var properties = new AuthenticationProperties(new Dictionary<string, string?>
        {
            [AuthConstants.ProviderNameProperty] = AuthConstants.GitHubProviderName,
            [AuthConstants.ReturnUrlProperty] = safeReturnUrl,
        })
        {
            RedirectUri = callbackPath,
        };

        return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet("/auth/github/callback")]
    public async Task<IActionResult> GitHubCallback(CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        string? requestedReturnUrl = null;
        result.Properties?.Items.TryGetValue(AuthConstants.ReturnUrlProperty, out requestedReturnUrl);
        var returnUrl = GetSafeReturnUrl(requestedReturnUrl);
        if (!result.Succeeded || result.Principal == null)
        {
            logger.LogWarning("GitHub login callback did not produce an authenticated principal.");
            return RedirectToLogin("github-auth-failed", returnUrl);
        }

        var accessToken = result.Properties?.GetTokenValue(AuthConstants.BackchannelAccessToken)
            ?? result.Properties?.GetTokenValue("access_token")
            ?? result.Properties?.GetTokenValue("token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogWarning("GitHub login callback did not include a usable access token.");
            return RedirectToLogin("missing-access-token", returnUrl);
        }

        var user = await gitHubUserMembershipService.ValidateOrganizationMemberAsync(
            accessToken,
            cancellationToken);
        if (user == null)
        {
            return RedirectToLogin("organization-required", returnUrl);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Login),
            new(AuthConstants.GitHubUserIdClaim, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(AuthConstants.GitHubLoginClaim, user.Login),
            new(AuthConstants.GitHubOrganizationClaim, gitHubUserMembershipService.Organization),
        };

        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.Name));
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            claims.Add(new Claim(AuthConstants.GitHubAvatarUrlClaim, user.AvatarUrl));
        }

        if (!string.IsNullOrWhiteSpace(user.HtmlUrl))
        {
            claims.Add(new Claim(AuthConstants.GitHubHtmlUrlClaim, user.HtmlUrl));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14),
            });

        return LocalRedirect(returnUrl);
    }

    [AllowAnonymous]
    [HttpGet("/api/v1/auth/me")]
    public ActionResult<CurrentUserResponse> Me()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(CurrentUserResponse.Anonymous());
        }

        return Ok(new CurrentUserResponse(
            true,
            User.FindFirstValue(AuthConstants.GitHubUserIdClaim) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.FindFirstValue(AuthConstants.GitHubLoginClaim) ?? User.Identity.Name,
            User.FindFirstValue(ClaimTypes.GivenName),
            User.FindFirstValue(AuthConstants.GitHubAvatarUrlClaim),
            User.FindFirstValue(AuthConstants.GitHubHtmlUrlClaim),
            User.FindFirstValue(AuthConstants.GitHubOrganizationClaim)));
    }

    [Authorize]
    [HttpPost("/api/v1/auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private string GetGitHubCallbackPath()
    {
        var callbackPath = configuration["Authentication:GitHub:CallbackPath"];
        if (string.IsNullOrWhiteSpace(callbackPath))
        {
            return "/auth/github/callback";
        }

        return callbackPath.StartsWith('/') ? callbackPath : $"/{callbackPath}";
    }

    private string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return "/";
        }

        return returnUrl;
    }

    private IActionResult RedirectToLogin(string error, string returnUrl) =>
        Redirect($"/auth/login?error={Uri.EscapeDataString(error)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
}

public sealed record CurrentUserResponse(
    bool IsAuthenticated,
    string? Id,
    string? Login,
    string? Name,
    string? AvatarUrl,
    string? HtmlUrl,
    string? Organization)
{
    public static CurrentUserResponse Anonymous() =>
        new(false, null, null, null, null, null, null);
}
