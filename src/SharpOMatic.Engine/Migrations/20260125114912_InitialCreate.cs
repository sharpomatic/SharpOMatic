using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "SharpOMatic");

            migrationBuilder.CreateTable(
                name: "ConnectorConfigMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConfigId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorConfigMetadata", x => x.ConfigId);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorMetadata", x => x.ConnectorId);
                });

            migrationBuilder.CreateTable(
                name: "ModelConfigMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConfigId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelConfigMetadata", x => x.ConfigId);
                });

            migrationBuilder.CreateTable(
                name: "ModelMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ModelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelMetadata", x => x.ModelId);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "SharpOMatic",
                columns: table => new
                {
                    SettingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    SettingType = table.Column<int>(type: "INTEGER", nullable: false),
                    UserEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValueString = table.Column<string>(type: "TEXT", nullable: true),
                    ValueBoolean = table.Column<bool>(type: "INTEGER", nullable: true),
                    ValueInteger = table.Column<int>(type: "INTEGER", nullable: true),
                    ValueDouble = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.SettingId);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                schema: "SharpOMatic",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Named = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Nodes = table.Column<string>(type: "TEXT", nullable: false),
                    Connections = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.WorkflowId);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                schema: "SharpOMatic",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RunStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Started = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Stopped = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InputEntries = table.Column<string>(type: "TEXT", nullable: true),
                    InputContext = table.Column<string>(type: "TEXT", nullable: true),
                    OutputContext = table.Column<string>(type: "TEXT", nullable: true),
                    CustomData = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_Runs_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "SharpOMatic",
                        principalTable: "Workflows",
                        principalColumn: "WorkflowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "SharpOMatic",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_Assets_Runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "Runs",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Traces",
                schema: "SharpOMatic",
                columns: table => new
                {
                    TraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ThreadId = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NodeType = table.Column<int>(type: "INTEGER", nullable: false),
                    NodeStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Finished = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InputContext = table.Column<string>(type: "TEXT", nullable: true),
                    OutputContext = table.Column<string>(type: "TEXT", nullable: true),
                    CustomData = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traces", x => x.TraceId);
                    table.ForeignKey(
                        name: "FK_Traces_Runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "Runs",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Name",
                schema: "SharpOMatic",
                table: "Assets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_RunId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Scope_Created",
                schema: "SharpOMatic",
                table: "Assets",
                columns: new[] { "Scope", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_WorkflowId_Created",
                schema: "SharpOMatic",
                table: "Runs",
                columns: new[] { "WorkflowId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_WorkflowId_RunStatus",
                schema: "SharpOMatic",
                table: "Runs",
                columns: new[] { "WorkflowId", "RunStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Traces_RunId_Created",
                schema: "SharpOMatic",
                table: "Traces",
                columns: new[] { "RunId", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assets",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "ConnectorConfigMetadata",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "ConnectorMetadata",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "ModelConfigMetadata",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "ModelMetadata",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Settings",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Traces",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Runs",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Workflows",
                schema: "SharpOMatic");
        }
    }
}
