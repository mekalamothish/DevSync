using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamRichTextCopies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentFormat",
                table: "TeamWorkspaceItems",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "markdown");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeamWorkspaceItems_ContentFormat",
                table: "TeamWorkspaceItems",
                sql: "\"ContentFormat\" IN ('markdown','richtext')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TeamWorkspaceItems_ContentFormat",
                table: "TeamWorkspaceItems");

            migrationBuilder.DropColumn(
                name: "ContentFormat",
                table: "TeamWorkspaceItems");
        }
    }
}
