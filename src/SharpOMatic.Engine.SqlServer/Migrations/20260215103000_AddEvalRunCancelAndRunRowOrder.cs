using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddEvalRunCancelAndRunRowOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [runRows]
                SET [Order] = ISNULL([rows].[Order], 0)
                FROM [SharpOMatic].[EvalRunRows] AS [runRows]
                LEFT JOIN [SharpOMatic].[EvalRows] AS [rows]
                    ON [rows].[EvalRowId] = [runRows].[EvalRowId];
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
