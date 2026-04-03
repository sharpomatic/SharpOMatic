using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Conversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConversationEnabled",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TurnNumber",
                schema: "SharpOMatic",
                table: "Runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Updated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentTurnNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    LeaseOwner = table.Column<string>(type: "TEXT", nullable: true),
                    LeaseExpires = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_Conversations_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "SharpOMatic",
                        principalTable: "Workflows",
                        principalColumn: "WorkflowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationCheckpoints",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResumeMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ResumeNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContextJson = table.Column<string>(type: "TEXT", nullable: true),
                    ResumeStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowSnapshotsJson = table.Column<string>(type: "TEXT", nullable: true),
                    GosubStackJson = table.Column<string>(type: "TEXT", nullable: true),
                    CheckpointCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceRunId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationCheckpoints", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_ConversationCheckpoints_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "SharpOMatic",
                        principalTable: "Conversations",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WorkflowId",
                schema: "SharpOMatic",
                table: "Conversations",
                column: "WorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationCheckpoints",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "SharpOMatic");

            migrationBuilder.DropColumn(
                name: "IsConversationEnabled",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "TurnNumber",
                schema: "SharpOMatic",
                table: "Runs");
        }
    }
}
