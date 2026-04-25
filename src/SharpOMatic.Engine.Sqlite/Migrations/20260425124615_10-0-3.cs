using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _1003 : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsEditorEvents",
                schema: "SharpOMatic",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TurnNumber",
                schema: "SharpOMatic",
                table: "Runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRunRows",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInScore",
                schema: "SharpOMatic",
                table: "EvalGraders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RowScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
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
                name: "StreamEvents",
                schema: "SharpOMatic",
                columns: table => new
                {
                    StreamEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", nullable: true),
                    MessageRole = table.Column<int>(type: "INTEGER", nullable: true),
                    ActivityType = table.Column<string>(type: "TEXT", nullable: true),
                    Replace = table.Column<bool>(type: "INTEGER", nullable: true),
                    TextDelta = table.Column<string>(type: "TEXT", nullable: true),
                    ToolCallId = table.Column<string>(type: "TEXT", nullable: true),
                    ParentMessageId = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    HideFromReply = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamEvents", x => x.StreamEventId);
                    table.ForeignKey(
                        name: "FK_StreamEvents_Runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "Runs",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationCheckpoints",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
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
                name: "IX_Runs_ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WorkflowId",
                schema: "SharpOMatic",
                table: "Conversations",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_ConversationId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "ConversationId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_RunId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "RunId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_WorkflowId_Created",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "WorkflowId", "Created" });

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Runs_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_Runs_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropTable(
                name: "ConversationCheckpoints",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "StreamEvents",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "SharpOMatic");

            migrationBuilder.DropIndex(
                name: "IX_Runs_ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ConversationId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IsConversationEnabled",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "NeedsEditorEvents",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "TurnNumber",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRunRows");

            migrationBuilder.DropColumn(
                name: "IncludeInScore",
                schema: "SharpOMatic",
                table: "EvalGraders");

            migrationBuilder.DropColumn(
                name: "RowScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Assets");
        }
    }
}
