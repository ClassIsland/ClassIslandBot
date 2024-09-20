using ClassIslandBot;
using ClassIslandBot.Services;
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

builder.Services.AddDbContext<BotContext>();

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

app.UseHttpsRedirection();
app.MapGitHubWebhooks();

app.Run();