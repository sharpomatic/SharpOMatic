using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ExpandEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArgsDelta",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentMessageId",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolCallId",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolCallName",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolResultContent",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArgsDelta",
                schema: "SharpOMatic",
                table: "StreamEvents");

            migrationBuilder.DropColumn(
                name: "ParentMessageId",
                schema: "SharpOMatic",
                table: "StreamEvents");

            migrationBuilder.DropColumn(
                name: "ToolCallId",
                schema: "SharpOMatic",
                table: "StreamEvents");

            migrationBuilder.DropColumn(
                name: "ToolCallName",
                schema: "SharpOMatic",
                table: "StreamEvents");

            migrationBuilder.DropColumn(
                name: "ToolResultContent",
                schema: "SharpOMatic",
                table: "StreamEvents");
        }
    }
}
