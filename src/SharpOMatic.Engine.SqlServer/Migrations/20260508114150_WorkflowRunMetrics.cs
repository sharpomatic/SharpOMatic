using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowRunMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowRunMetrics",
                schema: "SharpOMatic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Finished = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Duration = table.Column<long>(type: "bigint", nullable: true),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkflowVersion = table.Column<int>(type: "int", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    RunStatus = table.Column<int>(type: "int", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailedNodeEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailedNodeTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailedNodeType = table.Column<int>(type: "int", nullable: true),
                    ConversationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TurnNumber = table.Column<int>(type: "int", nullable: true),
                    IsConversationRun = table.Column<bool>(type: "bit", nullable: false),
                    ModelCallCount = table.Column<int>(type: "int", nullable: false),
                    ModelCallFailureCount = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false),
                    TotalModelCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRunMetrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_ConversationId_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                columns: new[] { "ConversationId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_RunId",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_RunStatus_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                columns: new[] { "RunStatus", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_Succeeded_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                columns: new[] { "Succeeded", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_WorkflowId_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                columns: new[] { "WorkflowId", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowRunMetrics",
                schema: "SharpOMatic");
        }
    }
}
