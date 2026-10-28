using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudyNotesAndRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudyNotebooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyNotebooks", x => x.Id);
                    table.UniqueConstraint("AK_StudyNotebooks_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_StudyNotebooks_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotebookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyNotes", x => x.Id);
                    table.UniqueConstraint("AK_StudyNotes_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_StudyNotes_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyNotes_StudyNotebooks_NotebookId_OwnerId",
                        columns: x => new { x.NotebookId, x.OwnerId },
                        principalTable: "StudyNotebooks",
                        principalColumns: new[] { "Id", "OwnerId" });
                });

            migrationBuilder.CreateTable(
                name: "RevisionCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    LapseCount = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionCards", x => x.Id);
                    table.UniqueConstraint("AK_RevisionCards_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.CheckConstraint("CK_RevisionCards_Schedule", "\"IntervalDays\" BETWEEN 0 AND 365 AND \"ReviewCount\" >= 0 AND \"LapseCount\" >= 0 AND \"LapseCount\" <= \"ReviewCount\"");
                    table.ForeignKey(
                        name: "FK_RevisionCards_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RevisionCards_StudyNotes_NoteId_OwnerId",
                        columns: x => new { x.NoteId, x.OwnerId },
                        principalTable: "StudyNotes",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyNoteBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Content = table.Column<string>(type: "character varying(60000)", maxLength: 60000, nullable: false),
                    Language = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LinkedPostId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyNoteBlocks", x => x.Id);
                    table.CheckConstraint("CK_StudyNoteBlocks_Article", "(\"Kind\" = 'article') OR \"LinkedPostId\" IS NULL");
                    table.CheckConstraint("CK_StudyNoteBlocks_Kind", "\"Kind\" IN ('paragraph', 'heading', 'code', 'checklist', 'article')");
                    table.CheckConstraint("CK_StudyNoteBlocks_Position", "\"Position\" >= 0 AND \"Position\" < 50");
                    table.ForeignKey(
                        name: "FK_StudyNoteBlocks_BlogPosts_LinkedPostId",
                        column: x => x.LinkedPostId,
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudyNoteBlocks_StudyNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "StudyNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyNoteTags",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyNoteTags", x => new { x.NoteId, x.Name });
                    table.ForeignKey(
                        name: "FK_StudyNoteTags_StudyNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "StudyNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevisionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    PreviousDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionReviews", x => x.Id);
                    table.CheckConstraint("CK_RevisionReviews_GradeAndInterval", "\"Grade\" BETWEEN 0 AND 3 AND \"IntervalDays\" BETWEEN 0 AND 365 AND \"NextDueAt\" > \"ReviewedAt\"");
                    table.ForeignKey(
                        name: "FK_RevisionReviews_RevisionCards_CardId_OwnerId",
                        columns: x => new { x.CardId, x.OwnerId },
                        principalTable: "RevisionCards",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionCards_NoteId_OwnerId",
                table: "RevisionCards",
                columns: new[] { "NoteId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionCards_OwnerId_DueAt_Id",
                table: "RevisionCards",
                columns: new[] { "OwnerId", "DueAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionReviews_CardId_OwnerId",
                table: "RevisionReviews",
                columns: new[] { "CardId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionReviews_CardId_SubmissionId",
                table: "RevisionReviews",
                columns: new[] { "CardId", "SubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionReviews_CardId_SubmittedVersion",
                table: "RevisionReviews",
                columns: new[] { "CardId", "SubmittedVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionReviews_OwnerId_ReviewedAt_Id",
                table: "RevisionReviews",
                columns: new[] { "OwnerId", "ReviewedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyNoteBlocks_LinkedPostId",
                table: "StudyNoteBlocks",
                column: "LinkedPostId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyNoteBlocks_NoteId_Position",
                table: "StudyNoteBlocks",
                columns: new[] { "NoteId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyNotebooks_OwnerId_NormalizedName",
                table: "StudyNotebooks",
                columns: new[] { "OwnerId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyNotes_NotebookId_OwnerId",
                table: "StudyNotes",
                columns: new[] { "NotebookId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyNotes_OwnerId_IsArchived_UpdatedAt",
                table: "StudyNotes",
                columns: new[] { "OwnerId", "IsArchived", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyNoteTags_Name",
                table: "StudyNoteTags",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevisionReviews");

            migrationBuilder.DropTable(
                name: "StudyNoteBlocks");

            migrationBuilder.DropTable(
                name: "StudyNoteTags");

            migrationBuilder.DropTable(
                name: "RevisionCards");

            migrationBuilder.DropTable(
                name: "StudyNotes");

            migrationBuilder.DropTable(
                name: "StudyNotebooks");
        }
    }
}
