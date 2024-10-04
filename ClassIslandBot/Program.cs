using ClassIslandBot;
using ClassIslandBot.Abstractions;
using ClassIslandBot.Models;
using ClassIslandBot.Services;
using ClassIslandBot.Services.Webhooks;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Core.Builders;
using Octokit.GraphQL.Model;
using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;
using static Octokit.GraphQL.Variable;
using ProductHeaderValue = Octokit.ProductHeaderValue;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => 
{
    if (!int.TryParse(builder.Configuration["QueueCapacity"], out var queueCapacity))
    {
        queueCapacity = 100;
    }

    return new IssueProcessBackgroundTaskQueue(queueCapacity);
});

builder.Services.AddDbContext<BotContext>();
builder.WebHost.UseSentry();

#if DEBUG
builder.Logging.SetMinimumLevel(LogLevel.Trace);
#endif

var app = builder.Build();

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
app.UseSentryTracing();

#if DEBUG  // 处于开发环境时需要自动迁移
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotContext>();
    db.Database.Migrate();
}
#endif

using (var scope = app.Services.CreateScope())
{
    var discussion = scope.ServiceProvider.GetService<DiscussionService>();
    if (discussion != null)
    {
        await discussion.SyncUnConnectedIssuesAsync();
    }
}

app.Run();