using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassIslandBot.Migrations
{
    /// <inheritdoc />
    public partial class AddIsTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTracking",
                table: "DiscussionAssociations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTracking",
                table: "DiscussionAssociations");
        }
    }
}
