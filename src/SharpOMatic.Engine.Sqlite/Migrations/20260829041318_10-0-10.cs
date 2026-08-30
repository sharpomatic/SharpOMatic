using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _10010 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModelCallCount",
                schema: "SharpOMatic",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalModelCost",
                schema: "SharpOMatic",
                table: "Runs",
                type: "TEXT",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ModelCallCount",
                schema: "SharpOMatic",
                table: "Conversations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalModelCost",
                schema: "SharpOMatic",
                table: "Conversations",
                type: "TEXT",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_ModelCallMetrics_RunId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelCallMetrics_RunId",
                schema: "SharpOMatic",
                table: "ModelCallMetrics");

            migrationBuilder.DropColumn(
                name: "ModelCallCount",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "TotalModelCost",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "ModelCallCount",
                schema: "SharpOMatic",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "TotalModelCost",
                schema: "SharpOMatic",
                table: "Conversations");
        }
    }
}
