using System.ClientModel;
using ClassIslandBot;
using ClassIslandBot.Abstractions;
using ClassIslandBot.Models;
using ClassIslandBot.Services;
using ClassIslandBot.Services.Authentication;
using ClassIslandBot.Services.Webhooks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Core.Builders;
using Octokit.GraphQL.Model;
using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;
using OpenAI;
using OpenAI.Chat;
using OpenIddict.Client.AspNetCore;
using static Octokit.GraphQL.Variable;
using ProductHeaderValue = Octokit.ProductHeaderValue;

var builder = WebApplication.CreateBuilder(args);
var githubCallbackPath = NormalizeCallbackPath(
    builder.Configuration["Authentication:GitHub:CallbackPath"] ?? "/auth/github/callback");
var openIddictGitHubCallbackUri = githubCallbackPath.TrimStart('/');

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<GitHubAuthService>();
builder.Services.AddScoped<DiscussionService>();
builder.Services.AddScoped<WebhookEventProcessor, IssueWebhookProcessorService>();
// builder.Services.AddScoped<WebhookEventProcessor, ReleaseWebhookProcessorService>();
builder.Services.AddScoped<ReleaseTrackingService>();
builder.Services.AddHostedService<IssueProcessBackgroundWorker>();
builder.Services.AddScoped<IssueCommandProcessService>();
builder.Services.AddSingleton<GithubOperationService>();
builder.Services.AddScoped<GitHubUserMembershipService>();
builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => 
{
    if (!int.TryParse(builder.Configuration["QueueCapacity"], out var queueCapacity))
    {
        queueCapacity = 100;
    }

    return new IssueProcessBackgroundTaskQueue(queueCapacity);
});
builder.Services.AddSingleton<OpenAIClient>(_ => new OpenAIClient(new ApiKeyCredential(builder.Configuration["OpenAIKey"] ?? ""), new OpenAIClientOptions()
{
    Endpoint = new Uri(builder.Configuration["OpenAIEndPoint"] ?? "")
}));
builder.Services.AddSingleton<IssueLabelService>();

builder.Services.AddDbContext<BotContext>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".ClassIslandBot.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context => HandleApiCookieRedirectAsync(context, StatusCodes.Status401Unauthorized),
            OnRedirectToAccessDenied = context => HandleApiCookieRedirectAsync(context, StatusCodes.Status403Forbidden),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddOpenIddict()
    .AddClient(options =>
    {
        options.AllowAuthorizationCodeFlow();
        options.DisableTokenStorage();
        options.SetRedirectionEndpointUris(openIddictGitHubCallbackUri);

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }
        else
        {
            options.AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();
        }

        options.UseSystemNetHttp();
        var aspNetCore = options.UseAspNetCore()
            .EnableRedirectionEndpointPassthrough();
        if (builder.Environment.IsDevelopment())
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }

        options.UseWebProviders()
            .AddGitHub(github =>
            {
                github.SetProviderName(AuthConstants.GitHubProviderName);
                github.SetRegistrationId(AuthConstants.GitHubRegistrationId);
                github.SetClientId(GetRequiredConfigurationValue("Authentication:GitHub:ClientId"));
                github.SetClientSecret(GetRequiredConfigurationValue("Authentication:GitHub:ClientSecret"));
                github.SetRedirectUri(openIddictGitHubCallbackUri);
                github.AddScopes("read:user", "read:org");
            });
    });
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Program).Assembly);
builder.WebHost.UseSentry();

#if DEBUG
builder.Logging.SetMinimumLevel(LogLevel.Trace);
#endif

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var github = app.Services.GetService<GitHubAuthService>();
if (github != null)
{
    Console.WriteLine(await github.GetInstallationTokenAsync());
}

// app.UseHttpsRedirection();
app.MapGitHubWebhooks(secret:app.Configuration["WebhookSecret"] ?? "");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseSentryTracing();

#if DEBUG  // 处于开发环境时需要自动迁移
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotContext>();
    db.Database.Migrate();
}
#endif


if (args.Length > 0 && args[0] == "migrate")
{
    using var scope = app.Services.CreateScope();
    var discussion = scope.ServiceProvider.GetService<DiscussionService>();
    if (discussion != null)
    {
        await discussion.MigrateDiscussions();
    }

    return;
}

using (var scope = app.Services.CreateScope())
{
    var discussion = scope.ServiceProvider.GetService<DiscussionService>();
    if (discussion != null)
    {
        await discussion.SyncUnConnectedIssuesAsync();
    }
}
app.MapFallbackToFile("/index.html");
app.Run();

static string NormalizeCallbackPath(string callbackPath)
{
    if (string.IsNullOrWhiteSpace(callbackPath))
    {
        return "/auth/github/callback";
    }

    return callbackPath.StartsWith('/') ? callbackPath : $"/{callbackPath}";
}

string GetRequiredConfigurationValue(string key)
{
    var value = builder.Configuration[key];
    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"Missing required configuration value: {key}.")
        : value;
}

static Task HandleApiCookieRedirectAsync(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}
