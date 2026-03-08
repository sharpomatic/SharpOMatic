using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class EvalRunAveragePassRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AveragePassRate",
                schema: "SharpOMatic",
                table: "EvalRuns",
                type: "float",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [runs]
                SET [AveragePassRate] = [scores].[AveragePassRate]
                FROM [SharpOMatic].[EvalRuns] AS [runs]
                OUTER APPLY
                (
                    SELECT AVG([summaries].[PassRate]) AS [AveragePassRate]
                    FROM [SharpOMatic].[EvalRunGraderSummaries] AS [summaries]
                    WHERE [summaries].[EvalRunId] = [runs].[EvalRunId]
                        AND [summaries].[PassRate] IS NOT NULL
                ) AS [scores]
                WHERE [scores].[AveragePassRate] IS NOT NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AveragePassRate",
                schema: "SharpOMatic",
                table: "EvalRuns");
        }
    }
}
