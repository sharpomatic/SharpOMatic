using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class _1007 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "FailureCategory",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogicalCallId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ProviderStatusCode",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repeat",
                schema: "SharpOMatic",
                table: "EvalRows",
                type: "int",
                nullable: true,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_LogicalCallId_AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "LogicalCallId", "AttemptNumber" });

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "ModelMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ModelMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "ConnectorMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ConnectorMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Assets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "ModelMetadata");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ModelMetadata");

            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "ConnectorMetadata");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ConnectorMetadata");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_ModelCallMetrics_LogicalCallId_AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "LogicalCallId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "ProviderStatusCode",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "Repeat",
                schema: "SharpOMatic",
                table: "EvalRows");
        }
    }
}
