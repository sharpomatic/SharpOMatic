using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
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
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TurnNumber",
                schema: "SharpOMatic",
                table: "Runs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentTurnNumber = table.Column<int>(type: "int", nullable: false),
                    LastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaseExpires = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeMode = table.Column<int>(type: "int", nullable: false),
                    ResumeNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResumeStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowSnapshotsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GosubStackJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckpointCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
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
