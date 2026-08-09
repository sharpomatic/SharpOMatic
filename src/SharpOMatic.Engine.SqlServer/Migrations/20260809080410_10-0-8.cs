using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class _1008 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgUiOutputPath",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeAgUiOutput",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgUiOutputPath",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "IncludeAgUiOutput",
                schema: "SharpOMatic",
                table: "EvalConfigs");
        }
    }
}
