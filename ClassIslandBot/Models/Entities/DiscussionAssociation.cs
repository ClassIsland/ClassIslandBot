using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClassIslandBot.Models.Entities;

public class DiscussionAssociation
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    
    public string RepoId { get; set; } = "";
    
    public string DiscussionId { get; set; } = "";

    public string IssueId { get; set; } = "";

    public bool IsTracking { get; set; } = true;
}