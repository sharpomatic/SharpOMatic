using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class EvalRowScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RowScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInScore",
                schema: "SharpOMatic",
                table: "EvalGraders",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRunRows",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "IncludeInScore",
                schema: "SharpOMatic",
                table: "EvalGraders");

            migrationBuilder.DropColumn(
                name: "RunScoreMode",
                schema: "SharpOMatic",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "SharpOMatic",
                table: "EvalRunRows");
        }
    }
}
