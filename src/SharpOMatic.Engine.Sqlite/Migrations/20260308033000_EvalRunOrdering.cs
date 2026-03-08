using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    [DbContext(typeof(SharpOMaticDbContext))]
    [Migration("20260308033000_EvalRunOrdering")]
    public class EvalRunOrdering : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EvalRuns_EvalConfigId_Order",
                schema: "SharpOMatic",
                table: "EvalRuns",
                columns: new[] { "EvalConfigId", "Order" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EvalRuns_EvalConfigId_Order",
                schema: "SharpOMatic",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "Order",
                schema: "SharpOMatic",
                table: "EvalRuns");
        }
    }
}
