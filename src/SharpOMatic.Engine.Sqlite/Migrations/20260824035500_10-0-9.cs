using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _1009 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChatMessagesPath",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeChatMessages",
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
                name: "ChatMessagesPath",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "IncludeChatMessages",
                schema: "SharpOMatic",
                table: "EvalConfigs");
        }
    }
}
