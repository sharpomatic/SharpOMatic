using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Duration = table.Column<long>(type: "bigint", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NodeEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConnectorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConnectorConfigId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConnectorConfigName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelConfigId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelConfigName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: true),
                    InputCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    OutputCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true)
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
