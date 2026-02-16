using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    [DbContext(typeof(SharpOMaticDbContext))]
    [Migration("20260215103000_AddEvalRunCancelAndRunRowOrder")]
    public class AddEvalRunCancelAndRunRowOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "CancelRequested", schema: "SharpOMatic", table: "EvalRuns", type: "INTEGER", nullable: false, defaultValue: false);

            migrationBuilder.AddColumn<int>(name: "Order", schema: "SharpOMatic", table: "EvalRunRows", type: "INTEGER", nullable: false, defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "EvalRunRows"
                SET "Order" = COALESCE(
                    (SELECT "Order" FROM "EvalRows" AS "rows" WHERE "rows"."EvalRowId" = "EvalRunRows"."EvalRowId"),
                    0
                );
                """
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CancelRequested", schema: "SharpOMatic", table: "EvalRuns");

            migrationBuilder.DropColumn(name: "Order", schema: "SharpOMatic", table: "EvalRunRows");
        }
    }
}
