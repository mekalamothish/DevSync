using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RichTextEditors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyNoteBlocks_Kind",
                table: "StudyNoteBlocks");

            migrationBuilder.AddColumn<string>(
                name: "BlocksJson",
                table: "BlogPosts",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "EditorFormat",
                table: "BlogPosts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "markdown");

            migrationBuilder.AddColumn<string>(
                name: "PublishedBlocksJson",
                table: "BlogPosts",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "PublishedEditorFormat",
                table: "BlogPosts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "markdown");

            migrationBuilder.AddColumn<string>(
                name: "PublishedRichText",
                table: "BlogPosts",
                type: "character varying(60000)",
                maxLength: 60000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RichText",
                table: "BlogPosts",
                type: "character varying(60000)",
                maxLength: 60000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyNoteBlocks_Kind",
                table: "StudyNoteBlocks",
                sql: "\"Kind\" IN ('paragraph', 'heading', 'code', 'checklist', 'article', 'richtext')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BlogPosts_EditorFormats",
                table: "BlogPosts",
                sql: "\"EditorFormat\" IN ('markdown','blocks','richtext') AND \"PublishedEditorFormat\" IN ('markdown','blocks','richtext')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyNoteBlocks_Kind",
                table: "StudyNoteBlocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BlogPosts_EditorFormats",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "BlocksJson",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "EditorFormat",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "PublishedBlocksJson",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "PublishedEditorFormat",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "PublishedRichText",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "RichText",
                table: "BlogPosts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyNoteBlocks_Kind",
                table: "StudyNoteBlocks",
                sql: "\"Kind\" IN ('paragraph', 'heading', 'code', 'checklist', 'article')");
        }
    }
}
