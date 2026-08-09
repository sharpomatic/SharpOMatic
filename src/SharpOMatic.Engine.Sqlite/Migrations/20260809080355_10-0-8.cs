using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeAgUiOutput",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "INTEGER",
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
