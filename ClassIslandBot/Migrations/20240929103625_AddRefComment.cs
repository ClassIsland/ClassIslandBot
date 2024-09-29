using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassIslandBot.Migrations
{
    /// <inheritdoc />
    public partial class AddRefComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefCommentId",
                table: "DiscussionAssociations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefCommentId",
                table: "DiscussionAssociations");
        }
    }
}
