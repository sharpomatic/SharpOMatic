using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AssetFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                schema: "SharpOMatic",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssetFolders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetFolders", x => x.FolderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_FolderId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Scope_FolderId_Created",
                schema: "SharpOMatic",
                table: "Assets",
                columns: new[] { "Scope", "FolderId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetFolders_Name",
                schema: "SharpOMatic",
                table: "AssetFolders",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_AssetFolders_FolderId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "FolderId",
                principalSchema: "SharpOMatic",
                principalTable: "AssetFolders",
                principalColumn: "FolderId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_AssetFolders_FolderId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "AssetFolders",
                schema: "SharpOMatic");

            migrationBuilder.DropIndex(
                name: "IX_Assets_FolderId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_Scope_FolderId_Created",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderId",
                schema: "SharpOMatic",
                table: "Assets");
        }
    }
}
