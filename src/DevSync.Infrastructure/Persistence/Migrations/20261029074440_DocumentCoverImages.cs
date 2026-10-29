using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentCoverImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoverImageId",
                table: "StudyNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoverImageId",
                table: "BlogPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedCoverImageId",
                table: "BlogPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyNotes_CoverImageId",
                table: "StudyNotes",
                column: "CoverImageId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_CoverImageId",
                table: "BlogPosts",
                column: "CoverImageId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_PublishedCoverImageId",
                table: "BlogPosts",
                column: "PublishedCoverImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlogPosts_ImageAssets_CoverImageId",
                table: "BlogPosts",
                column: "CoverImageId",
                principalTable: "ImageAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogPosts_ImageAssets_PublishedCoverImageId",
                table: "BlogPosts",
                column: "PublishedCoverImageId",
                principalTable: "ImageAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyNotes_ImageAssets_CoverImageId",
                table: "StudyNotes",
                column: "CoverImageId",
                principalTable: "ImageAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlogPosts_ImageAssets_CoverImageId",
                table: "BlogPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_BlogPosts_ImageAssets_PublishedCoverImageId",
                table: "BlogPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyNotes_ImageAssets_CoverImageId",
                table: "StudyNotes");

            migrationBuilder.DropIndex(
                name: "IX_StudyNotes_CoverImageId",
                table: "StudyNotes");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_CoverImageId",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_PublishedCoverImageId",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "CoverImageId",
                table: "StudyNotes");

            migrationBuilder.DropColumn(
                name: "CoverImageId",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "PublishedCoverImageId",
                table: "BlogPosts");
        }
    }
}
