using System;
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
            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Workflows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "ModelMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ModelMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "EvalConfigs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                schema: "SharpOMatic",
                table: "ConnectorMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ConnectorMetadata",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Assets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "ModelMetadata");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ModelMetadata");

            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "EvalConfigs");

            migrationBuilder.DropColumn(
                name: "Created",
                schema: "SharpOMatic",
                table: "ConnectorMetadata");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "ConnectorMetadata");

            migrationBuilder.DropColumn(
                name: "Modified",
                schema: "SharpOMatic",
                table: "Assets");
        }
    }
}
