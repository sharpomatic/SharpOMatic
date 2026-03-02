using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class GraderInputContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputContext",
                schema: "SharpOMatic",
                table: "EvalRunRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputContext",
                schema: "SharpOMatic",
                table: "EvalRunRowGraders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputContext",
                schema: "SharpOMatic",
                table: "EvalRunRows");

            migrationBuilder.DropColumn(
                name: "InputContext",
                schema: "SharpOMatic",
                table: "EvalRunRowGraders");
        }
    }
}
