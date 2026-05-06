using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ModelCallMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelCallMetrics",
                schema: "SharpOMatic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<long>(type: "INTEGER", nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorType = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowName = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NodeEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConnectorName = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectorConfigId = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectorConfigName = table.Column<string>(type: "TEXT", nullable: true),
                    ModelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModelName = table.Column<string>(type: "TEXT", nullable: true),
                    ModelConfigId = table.Column<string>(type: "TEXT", nullable: true),
                    ModelConfigName = table.Column<string>(type: "TEXT", nullable: true),
                    ProviderModelName = table.Column<string>(type: "TEXT", nullable: true),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: true),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: true),
                    TotalTokens = table.Column<long>(type: "INTEGER", nullable: true),
                    InputCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    OutputCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    TotalCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCallMetrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_ConnectorId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "ConnectorId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_ConversationId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "ConversationId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_ModelId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "ModelId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_Succeeded_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "Succeeded", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_WorkflowId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "WorkflowId", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelCallMetrics",
                schema: "SharpOMatic");
        }
    }
}
