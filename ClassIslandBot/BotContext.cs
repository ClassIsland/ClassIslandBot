using ClassIslandBot.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassIslandBot;

public class BotContext : DbContext
{
    public DbSet<DiscussionAssociation> DiscussionAssociations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // TODO:使用 My SQL 数据库
        options.UseSqlite($"Data Source=data.db");
        base.OnConfiguring(options);
    }
}