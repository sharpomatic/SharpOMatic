using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _10012 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantIdPath",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserIdPath",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_TenantId_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                columns: new[] { "TenantId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRunMetrics_UserId_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics",
                columns: new[] { "UserId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_TenantId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "TenantId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_UserId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "UserId", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowRunMetrics_TenantId_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowRunMetrics_UserId_Created",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics");

            migrationBuilder.DropIndex(
                name: "IX_ModelCallMetrics_TenantId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropIndex(
                name: "IX_ModelCallMetrics_UserId_Created",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "TenantIdPath",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "UserIdPath",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "SharpOMatic",
                table: "WorkflowRunMetrics");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");
        }
    }
}
