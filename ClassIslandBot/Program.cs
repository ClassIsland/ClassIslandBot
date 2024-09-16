using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Core.Builders;
using Octokit.GraphQL.Model;
using static Octokit.GraphQL.Variable;
using ProductHeaderValue = Octokit.ProductHeaderValue;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

var generator = new GitHubJwt.GitHubJwtFactory(
    new GitHubJwt.FilePrivateKeySource("./private-key.pem"),
    new GitHubJwt.GitHubJwtFactoryOptions
    {
        AppIntegrationId = 998668, // The GitHub App Id
        ExpirationSeconds = 120 // 10 minutes is the maximum time allowed
    }
);

var jwtToken = generator.CreateEncodedJwtToken();

var github = new GitHubClient(new ProductHeaderValue("classisland-bot"))
{
    Credentials = new Credentials(jwtToken, AuthenticationType.Bearer)
};
var installation = await github.GitHubApps.GetOrganizationInstallationForCurrent("ClassIsland");
var installationId = installation.Id;
var token = await github.GitHubApps.CreateInstallationToken(installationId);
var installApp = new GitHubClient(new Octokit.ProductHeaderValue("classisland-bot"))
{
    Credentials = new Credentials(token.Token)
};

var connection = new Octokit.GraphQL.Connection(new Octokit.GraphQL.ProductHeaderValue("classisland-bot"), token.Token);

var query = new Query()
    .RepositoryOwner(Var("owner"))
    .Repository(Var("name"))
    .Discussion(340)
    .Select(x => new
    {
        Id = x.Id,
    })
    .Compile();

var vars = new Dictionary<string, object>
{
    { "owner", "ClassIsland" },
    { "name", "ClassIsland" },
};

var result = await connection.Run(query, vars);



app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}