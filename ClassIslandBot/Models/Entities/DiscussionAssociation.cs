namespace ClassIslandBot.Models.Entities;

public class DiscussionAssociation
{
    public long RepoId { get; set; } = 0;
    
    public long DiscussionId { get; set; } = 0;

    public long IssueId { get; set; } = 0;
}