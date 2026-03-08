using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class EvalRunOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EvalRuns_EvalConfigId_Order",
                schema: "SharpOMatic",
                table: "EvalRuns",
                columns: new[] { "EvalConfigId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
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
