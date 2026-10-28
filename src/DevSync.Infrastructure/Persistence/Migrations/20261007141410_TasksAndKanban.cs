using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TasksAndKanban : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskBoards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskBoards", x => x.Id);
                    table.UniqueConstraint("AK_TaskBoards_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_TaskBoards_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskColumns", x => x.Id);
                    table.UniqueConstraint("AK_TaskColumns_Id_BoardId_OwnerId", x => new { x.Id, x.BoardId, x.OwnerId });
                    table.CheckConstraint("CK_TaskColumns_Key", "\"Key\" IN ('todo', 'in-progress', 'done')");
                    table.CheckConstraint("CK_TaskColumns_Position", "\"Position\" >= 0 AND \"Position\" <= 2");
                    table.ForeignKey(
                        name: "FK_TaskColumns_TaskBoards_BoardId_OwnerId",
                        columns: x => new { x.BoardId, x.OwnerId },
                        principalTable: "TaskBoards",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTasks", x => x.Id);
                    table.UniqueConstraint("AK_WorkTasks_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.CheckConstraint("CK_WorkTasks_Assignment", "\"AssignedUserId\" IS NULL OR \"AssignedUserId\" = \"OwnerId\"");
                    table.CheckConstraint("CK_WorkTasks_Position", "(\"IsArchived\" AND \"Position\" = -1) OR (NOT \"IsArchived\" AND \"Position\" >= 0 AND \"Position\" < 200)");
                    table.CheckConstraint("CK_WorkTasks_Priority", "\"Priority\" IN ('low', 'medium', 'high', 'urgent')");
                    table.ForeignKey(
                        name: "FK_WorkTasks_AspNetUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkTasks_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkTasks_BlogPosts_LinkedPostId",
                        column: x => x.LinkedPostId,
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkTasks_StudyNotes_LinkedNoteId_OwnerId",
                        columns: x => new { x.LinkedNoteId, x.OwnerId },
                        principalTable: "StudyNotes",
                        principalColumns: new[] { "Id", "OwnerId" });
                    table.ForeignKey(
                        name: "FK_WorkTasks_TaskBoards_BoardId_OwnerId",
                        columns: x => new { x.BoardId, x.OwnerId },
                        principalTable: "TaskBoards",
                        principalColumns: new[] { "Id", "OwnerId" });
                    table.ForeignKey(
                        name: "FK_WorkTasks_TaskColumns_ColumnId_BoardId_OwnerId",
                        columns: x => new { x.ColumnId, x.BoardId, x.OwnerId },
                        principalTable: "TaskColumns",
                        principalColumns: new[] { "Id", "BoardId", "OwnerId" });
                });

            migrationBuilder.CreateTable(
                name: "TaskActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskActivities_WorkTasks_TaskId_OwnerId",
                        columns: x => new { x.TaskId, x.OwnerId },
                        principalTable: "WorkTasks",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskChecklistItems", x => x.Id);
                    table.CheckConstraint("CK_TaskChecklistItems_Position", "\"Position\" >= 0 AND \"Position\" < 30");
                    table.ForeignKey(
                        name: "FK_TaskChecklistItems_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_TaskId_CreatedAt_Id",
                table: "TaskActivities",
                columns: new[] { "TaskId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_TaskId_OwnerId",
                table: "TaskActivities",
                columns: new[] { "TaskId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskBoards_OwnerId_CreatedAt",
                table: "TaskBoards",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskChecklistItems_TaskId_Position",
                table: "TaskChecklistItems",
                columns: new[] { "TaskId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskColumns_BoardId_Key",
                table: "TaskColumns",
                columns: new[] { "BoardId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskColumns_BoardId_OwnerId",
                table: "TaskColumns",
                columns: new[] { "BoardId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_AssignedUserId",
                table: "WorkTasks",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_BoardId_IsArchived_ColumnId_Position",
                table: "WorkTasks",
                columns: new[] { "BoardId", "IsArchived", "ColumnId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_BoardId_OwnerId",
                table: "WorkTasks",
                columns: new[] { "BoardId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_ColumnId_BoardId_OwnerId",
                table: "WorkTasks",
                columns: new[] { "ColumnId", "BoardId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_LinkedNoteId_OwnerId",
                table: "WorkTasks",
                columns: new[] { "LinkedNoteId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_LinkedPostId",
                table: "WorkTasks",
                column: "LinkedPostId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_OwnerId_IsArchived_DueDate",
                table: "WorkTasks",
                columns: new[] { "OwnerId", "IsArchived", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskActivities");

            migrationBuilder.DropTable(
                name: "TaskChecklistItems");

            migrationBuilder.DropTable(
                name: "WorkTasks");

            migrationBuilder.DropTable(
                name: "TaskColumns");

            migrationBuilder.DropTable(
                name: "TaskBoards");
        }
    }
}
