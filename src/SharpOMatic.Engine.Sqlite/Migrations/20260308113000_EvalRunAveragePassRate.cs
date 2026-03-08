using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    [DbContext(typeof(SharpOMaticDbContext))]
    [Migration("20260308113000_EvalRunAveragePassRate")]
    public class EvalRunAveragePassRate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AveragePassRate",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "REAL",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "EvalRuns"
                SET "AveragePassRate" = (
                    SELECT AVG("summaries"."PassRate")
                    FROM "EvalRunGraderSummaries" AS "summaries"
                    WHERE "summaries"."EvalRunId" = "EvalRuns"."EvalRunId"
                        AND "summaries"."PassRate" IS NOT NULL
                )
                WHERE EXISTS
                (
                    SELECT 1
                    FROM "EvalRunGraderSummaries" AS "summaries"
                    WHERE "summaries"."EvalRunId" = "EvalRuns"."EvalRunId"
                        AND "summaries"."PassRate" IS NOT NULL
                );
                """
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AveragePassRate",
                schema: "SharpOMatic",
                table: "EvalRuns");
        }
    }
}
