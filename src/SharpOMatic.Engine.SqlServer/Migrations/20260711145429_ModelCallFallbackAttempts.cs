using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ModelCallFallbackAttempts : Migration
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
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<int>(
                name: "ProviderStatusCode",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_LogicalCallId_AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "LogicalCallId", "AttemptNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelCallMetrics_LogicalCallId_AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(name: "AttemptNumber", schema: "SharpOMatic", table: "ModelCallMetrics");
            migrationBuilder.DropColumn(name: "FailureCategory", schema: "SharpOMatic", table: "ModelCallMetrics");
            migrationBuilder.DropColumn(name: "LogicalCallId", schema: "SharpOMatic", table: "ModelCallMetrics");
            migrationBuilder.DropColumn(name: "ProviderStatusCode", schema: "SharpOMatic", table: "ModelCallMetrics");
        }
    }
}
