using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    [Migration("20260504115215_RemoveConversationLease")]
    public partial class RemoveConversationLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaseExpires",
                schema: "SharpOMatic",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                schema: "SharpOMatic",
                table: "Conversations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpires",
                schema: "SharpOMatic",
                table: "Conversations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                schema: "SharpOMatic",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
