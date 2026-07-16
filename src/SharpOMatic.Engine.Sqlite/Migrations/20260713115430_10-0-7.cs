using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _1007 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowFolderId",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "FailureCategory",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogicalCallId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ProviderStatusCode",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repeat",
                schema: "SharpOMatic",
                table: "EvalRows",
                type: "INTEGER",
                nullable: true,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "WorkflowFolders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    WorkflowFolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowFolders", x => x.WorkflowFolderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_WorkflowFolderId",
                schema: "SharpOMatic",
                table: "Workflows",
                column: "WorkflowFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_LogicalCallId_AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                columns: new[] { "LogicalCallId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowFolders_Name",
                schema: "SharpOMatic",
                table: "WorkflowFolders",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Workflows_WorkflowFolders_WorkflowFolderId",
                schema: "SharpOMatic",
                table: "Workflows",
                column: "WorkflowFolderId",
                principalSchema: "SharpOMatic",
                principalTable: "WorkflowFolders",
                principalColumn: "WorkflowFolderId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Workflows_WorkflowFolders_WorkflowFolderId",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropTable(
                name: "WorkflowFolders",
                schema: "SharpOMatic");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_WorkflowFolderId",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_ModelCallMetrics_LogicalCallId_AttemptNumber",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "WorkflowFolderId",
                schema: "SharpOMatic",
                table: "Workflows");

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
