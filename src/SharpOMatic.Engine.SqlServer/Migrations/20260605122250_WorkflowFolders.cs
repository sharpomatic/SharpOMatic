using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "WorkflowFolderId", schema: "SharpOMatic", table: "Workflows", type: "uniqueidentifier", nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowFolders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    WorkflowFolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowFolders", x => x.WorkflowFolderId);
                }
            );

            migrationBuilder.CreateIndex(name: "IX_Workflows_WorkflowFolderId", schema: "SharpOMatic", table: "Workflows", column: "WorkflowFolderId");

            migrationBuilder.CreateIndex(name: "IX_WorkflowFolders_Name", schema: "SharpOMatic", table: "WorkflowFolders", column: "Name", unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Workflows_WorkflowFolders_WorkflowFolderId",
                schema: "SharpOMatic",
                table: "Workflows",
                column: "WorkflowFolderId",
                principalSchema: "SharpOMatic",
                principalTable: "WorkflowFolders",
                principalColumn: "WorkflowFolderId",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Workflows_WorkflowFolders_WorkflowFolderId", schema: "SharpOMatic", table: "Workflows");

            migrationBuilder.DropTable(name: "WorkflowFolders", schema: "SharpOMatic");

            migrationBuilder.DropIndex(name: "IX_Workflows_WorkflowFolderId", schema: "SharpOMatic", table: "Workflows");

            migrationBuilder.DropColumn(name: "WorkflowFolderId", schema: "SharpOMatic", table: "Workflows");
        }
    }
}
