using ClassIslandBot.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassIslandBot.Controllers;

[ApiController]
[Route("/api/v1/github")]
public class GitHubMetadataController(GithubOperationService githubOperationService) : ControllerBase
{
    [HttpGet("repositories")]
    public async Task<ActionResult<IReadOnlyList<GitHubRepositoryOption>>> GetRepositoriesAsync(
        [FromQuery] string? keyword = null)
    {
        return Ok(await githubOperationService.GetRepositoriesAsync(keyword));
    }

    [HttpGet("repositories/{repoId}/issues")]
    public async Task<ActionResult<IReadOnlyList<GitHubIssueOption>>> GetIssuesAsync(
        string repoId,
        [FromQuery] string? keyword = null,
        [FromQuery] int take = 50)
    {
        if (string.IsNullOrWhiteSpace(repoId))
        {
            return BadRequest("RepoId is required.");
        }

        return Ok(await githubOperationService.GetIssuesAsync(repoId, keyword, take));
    }

    [HttpGet("discussions")]
    public async Task<ActionResult<IReadOnlyList<GitHubDiscussionOption>>> GetDiscussionsAsync(
        [FromQuery] string? keyword = null,
        [FromQuery] int take = 50)
    {
        return Ok(await githubOperationService.GetVotingDiscussionsAsync(keyword, take));
    }
}
