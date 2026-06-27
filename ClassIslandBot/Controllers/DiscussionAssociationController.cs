using ClassIslandBot.Abstractions;
using ClassIslandBot.ComponentModels;
using ClassIslandBot.Models.Entities;
using ClassIslandBot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassIslandBot.Controllers;

[ApiController]
[Route("/api/v1/discussions")]
public class DiscussionAssociationsController(
    BotContext dbContext,
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory serviceScopeFactory,
    GithubOperationService githubOperationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<DiscussionAssociationResponse>>> ListAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? repoId = null,
        [FromQuery] string? discussionId = null,
        [FromQuery] string? issueId = null,
        [FromQuery] bool? isTracking = null,
        CancellationToken cancellationToken = default)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.DiscussionAssociations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(repoId))
        {
            query = query.Where(x => x.RepoId.Contains(repoId));
        }

        if (!string.IsNullOrWhiteSpace(discussionId))
        {
            var normalizedDiscussionId =
                await githubOperationService.NormalizeVotingDiscussionIdentifierAsync(discussionId);
            query = query.Where(x => x.DiscussionId.Contains(normalizedDiscussionId));
        }

        if (!string.IsNullOrWhiteSpace(issueId))
        {
            var normalizedIssueIds = await NormalizeIssueFilterAsync(query, repoId, issueId, cancellationToken);
            query = normalizedIssueIds == null
                ? query.Where(x => x.IssueId.Contains(issueId.Trim()))
                : query.Where(x => normalizedIssueIds.Contains(x.IssueId));
        }

        if (isTracking.HasValue)
        {
            query = query.Where(x => x.IsTracking == isTracking.Value);
        }

        return Ok(await PaginatedList<DiscussionAssociationResponse>.CreateAsync(
            query,
            pageIndex,
            pageSize,
            x => x.Id,
            x => new DiscussionAssociationResponse(
                x.Id,
                x.RepoId,
                x.DiscussionId,
                x.IssueId,
                x.RefCommentId,
                x.IsTracking),
            true));
    }

    [HttpGet("{id:long}", Name = nameof(GetAsync))]
    public async Task<ActionResult<DiscussionAssociationResponse>> GetAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var association = await dbContext.DiscussionAssociations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return association == null
            ? NotFound()
            : DiscussionAssociationResponse.FromEntity(association);
    }

    [HttpPost]
    public async Task<ActionResult<DiscussionAssociationResponse>> CreateAsync(
        DiscussionAssociationRequest request)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var normalizedRequest = await NormalizeRequestAsync(request);
        DiscussionAssociationResponse? result = null;
        await taskQueue.QueueBackgroundWorkItemAndWaitAsync(async cancellationToken =>
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<BotContext>();
            var association = new DiscussionAssociation();
            ApplyRequest(association, normalizedRequest);

            context.DiscussionAssociations.Add(association);
            await context.SaveChangesAsync(cancellationToken);
            result = DiscussionAssociationResponse.FromEntity(association);
        });

        return CreatedAtRoute(nameof(GetAsync), new { id = result!.Id }, result);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DiscussionAssociationResponse>> UpdateAsync(
        long id,
        DiscussionAssociationRequest request)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var normalizedRequest = await NormalizeRequestAsync(request);
        DiscussionAssociationResponse? result = null;
        await taskQueue.QueueBackgroundWorkItemAndWaitAsync(async cancellationToken =>
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<BotContext>();
            var association = await context.DiscussionAssociations
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (association == null)
            {
                return;
            }

            ApplyRequest(association, normalizedRequest);
            await context.SaveChangesAsync(cancellationToken);
            result = DiscussionAssociationResponse.FromEntity(association);
        });

        return result == null ? NotFound() : result;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync(long id)
    {
        var deleted = false;
        await taskQueue.QueueBackgroundWorkItemAndWaitAsync(async cancellationToken =>
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<BotContext>();
            var association = await context.DiscussionAssociations
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (association == null)
            {
                return;
            }

            context.DiscussionAssociations.Remove(association);
            await context.SaveChangesAsync(cancellationToken);
            deleted = true;
        });

        return deleted ? NoContent() : NotFound();
    }

    private static void ApplyRequest(DiscussionAssociation association, DiscussionAssociationRequest request)
    {
        association.RepoId = request.RepoId.Trim();
        association.DiscussionId = request.DiscussionId.Trim();
        association.IssueId = request.IssueId.Trim();
        association.RefCommentId = string.IsNullOrWhiteSpace(request.RefCommentId)
            ? null
            : request.RefCommentId.Trim();
        association.IsTracking = request.IsTracking;
    }

    private async Task<DiscussionAssociationRequest> NormalizeRequestAsync(DiscussionAssociationRequest request)
    {
        return request with
        {
            IssueId = await githubOperationService.NormalizeIssueIdentifierAsync(request.RepoId.Trim(), request.IssueId),
            DiscussionId = await githubOperationService.NormalizeVotingDiscussionIdentifierAsync(request.DiscussionId),
        };
    }

    private async Task<List<string>?> NormalizeIssueFilterAsync(
        IQueryable<DiscussionAssociation> query,
        string? repoId,
        string issueId,
        CancellationToken cancellationToken)
    {
        if (!TryParseNumberFilter(issueId, out _))
        {
            return null;
        }

        var repoIds = string.IsNullOrWhiteSpace(repoId)
            ? await query.Select(x => x.RepoId).Distinct().ToListAsync(cancellationToken)
            : [repoId.Trim()];

        var issueIds = new List<string>();
        foreach (var currentRepoId in repoIds.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var normalizedIssueId =
                await githubOperationService.NormalizeIssueIdentifierAsync(currentRepoId, issueId);
            if (normalizedIssueId != issueId.Trim())
            {
                issueIds.Add(normalizedIssueId);
            }
        }

        return issueIds;
    }

    private static bool TryParseNumberFilter(string keyword, out int number)
    {
        number = 0;
        var normalized = keyword.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        return int.TryParse(normalized, out number) && number > 0;
    }

    private BadRequestObjectResult? ValidateRequest(DiscussionAssociationRequest request)
    {
        Dictionary<string, string[]> errors = [];

        if (string.IsNullOrWhiteSpace(request.RepoId))
        {
            errors[nameof(request.RepoId)] = ["RepoId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.DiscussionId))
        {
            errors[nameof(request.DiscussionId)] = ["DiscussionId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.IssueId))
        {
            errors[nameof(request.IssueId)] = ["IssueId is required."];
        }

        return errors.Count == 0 ? null : BadRequest(errors);
    }
}

public record DiscussionAssociationRequest(
    string RepoId,
    string DiscussionId,
    string IssueId,
    string? RefCommentId,
    bool IsTracking = true);

public record DiscussionAssociationResponse(
    long Id,
    string RepoId,
    string DiscussionId,
    string IssueId,
    string? RefCommentId,
    bool IsTracking)
{
    public static DiscussionAssociationResponse FromEntity(DiscussionAssociation association) =>
        new(
            association.Id,
            association.RepoId,
            association.DiscussionId,
            association.IssueId,
            association.RefCommentId,
            association.IsTracking);
}
