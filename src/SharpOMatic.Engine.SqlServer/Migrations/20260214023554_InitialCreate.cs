using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "SharpOMatic");

            migrationBuilder.CreateTable(
                name: "ConnectorConfigMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConfigId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorConfigMetadata", x => x.ConfigId);
                }
            );

            migrationBuilder.CreateTable(
                name: "ConnectorMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorMetadata", x => x.ConnectorId);
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalConfigs",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxParallel = table.Column<int>(type: "int", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalConfigs", x => x.EvalConfigId);
                }
            );

            migrationBuilder.CreateTable(
                name: "ModelConfigMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ConfigId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelConfigMetadata", x => x.ConfigId);
                }
            );

            migrationBuilder.CreateTable(
                name: "ModelMetadata",
                schema: "SharpOMatic",
                columns: table => new
                {
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelMetadata", x => x.ModelId);
                }
            );

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "SharpOMatic",
                columns: table => new
                {
                    SettingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SettingType = table.Column<int>(type: "int", nullable: false),
                    UserEditable = table.Column<bool>(type: "bit", nullable: false),
                    ValueString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueBoolean = table.Column<bool>(type: "bit", nullable: true),
                    ValueInteger = table.Column<int>(type: "int", nullable: true),
                    ValueDouble = table.Column<double>(type: "float", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.SettingId);
                }
            );

            migrationBuilder.CreateTable(
                name: "Workflows",
                schema: "SharpOMatic",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Named = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nodes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Connections = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.WorkflowId);
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalColumns",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalColumnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    Optional = table.Column<bool>(type: "bit", nullable: false),
                    InputPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalColumns", x => x.EvalColumnId);
                    table.ForeignKey(
                        name: "FK_EvalColumns_EvalConfigs_EvalConfigId",
                        column: x => x.EvalConfigId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalConfigs",
                        principalColumn: "EvalConfigId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalGraders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalGraderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PassThreshold = table.Column<double>(type: "float", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalGraders", x => x.EvalGraderId);
                    table.ForeignKey(
                        name: "FK_EvalGraders_EvalConfigs_EvalConfigId",
                        column: x => x.EvalConfigId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalConfigs",
                        principalColumn: "EvalConfigId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalRows",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRows", x => x.EvalRowId);
                    table.ForeignKey(
                        name: "FK_EvalRows_EvalConfigs_EvalConfigId",
                        column: x => x.EvalConfigId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalConfigs",
                        principalColumn: "EvalConfigId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalRuns",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Finished = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancelRequested = table.Column<bool>(type: "bit", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    CompletedRows = table.Column<int>(type: "int", nullable: false),
                    FailedRows = table.Column<int>(type: "int", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRuns", x => x.EvalRunId);
                    table.ForeignKey(
                        name: "FK_EvalRuns_EvalConfigs_EvalConfigId",
                        column: x => x.EvalConfigId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalConfigs",
                        principalColumn: "EvalConfigId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Runs",
                schema: "SharpOMatic",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RunStatus = table.Column<int>(type: "int", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Stopped = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputEntries = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputContext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputContext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalData",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalColumnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StringValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IntValue = table.Column<int>(type: "int", nullable: true),
                    DoubleValue = table.Column<double>(type: "float", nullable: true),
                    BoolValue = table.Column<bool>(type: "bit", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalData", x => x.EvalDataId);
                    table.ForeignKey(
                        name: "FK_EvalData_EvalColumns_EvalColumnId",
                        column: x => x.EvalColumnId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalColumns",
                        principalColumn: "EvalColumnId",
                        onDelete: ReferentialAction.NoAction
                    );
                    table.ForeignKey(
                        name: "FK_EvalData_EvalRows_EvalRowId",
                        column: x => x.EvalRowId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRows",
                        principalColumn: "EvalRowId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalRunGraderSummaries",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunGraderSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalGraderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    CompletedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    MinScore = table.Column<double>(type: "float", nullable: true),
                    MaxScore = table.Column<double>(type: "float", nullable: true),
                    AverageScore = table.Column<double>(type: "float", nullable: true),
                    MedianScore = table.Column<double>(type: "float", nullable: true),
                    StandardDeviation = table.Column<double>(type: "float", nullable: true),
                    PassRate = table.Column<double>(type: "float", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRunGraderSummaries", x => x.EvalRunGraderSummaryId);
                    table.ForeignKey(
                        name: "FK_EvalRunGraderSummaries_EvalGraders_EvalGraderId",
                        column: x => x.EvalGraderId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalGraders",
                        principalColumn: "EvalGraderId",
                        onDelete: ReferentialAction.NoAction
                    );
                    table.ForeignKey(
                        name: "FK_EvalRunGraderSummaries_EvalRuns_EvalRunId",
                        column: x => x.EvalRunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRuns",
                        principalColumn: "EvalRunId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalRunRows",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Finished = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OutputContext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRunRows", x => x.EvalRunRowId);
                    table.ForeignKey(
                        name: "FK_EvalRunRows_EvalRows_EvalRowId",
                        column: x => x.EvalRowId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRows",
                        principalColumn: "EvalRowId",
                        onDelete: ReferentialAction.NoAction
                    );
                    table.ForeignKey(
                        name: "FK_EvalRunRows_EvalRuns_EvalRunId",
                        column: x => x.EvalRunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRuns",
                        principalColumn: "EvalRunId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "SharpOMatic",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Traces",
                schema: "SharpOMatic",
                columns: table => new
                {
                    TraceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentTraceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ThreadId = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NodeType = table.Column<int>(type: "int", nullable: false),
                    NodeStatus = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Finished = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputContext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputContext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EvalRunRowGraders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunRowGraderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalRunRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalGraderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvalRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Finished = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: true),
                    OutputContext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRunRowGraders", x => x.EvalRunRowGraderId);
                    table.ForeignKey(
                        name: "FK_EvalRunRowGraders_EvalGraders_EvalGraderId",
                        column: x => x.EvalGraderId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalGraders",
                        principalColumn: "EvalGraderId",
                        onDelete: ReferentialAction.NoAction
                    );
                    table.ForeignKey(
                        name: "FK_EvalRunRowGraders_EvalRunRows_EvalRunRowId",
                        column: x => x.EvalRunRowId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRunRows",
                        principalColumn: "EvalRunRowId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(name: "IX_Assets_Name", schema: "SharpOMatic", table: "Assets", column: "Name");

            migrationBuilder.CreateIndex(name: "IX_Assets_RunId", schema: "SharpOMatic", table: "Assets", column: "RunId");

            migrationBuilder.CreateIndex(name: "IX_Assets_Scope_Created", schema: "SharpOMatic", table: "Assets", columns: new[] { "Scope", "Created" });

            migrationBuilder.CreateIndex(name: "IX_EvalColumns_EvalConfigId", schema: "SharpOMatic", table: "EvalColumns", column: "EvalConfigId");

            migrationBuilder.CreateIndex(name: "IX_EvalData_EvalColumnId", schema: "SharpOMatic", table: "EvalData", column: "EvalColumnId");

            migrationBuilder.CreateIndex(name: "IX_EvalData_EvalRowId", schema: "SharpOMatic", table: "EvalData", column: "EvalRowId");

            migrationBuilder.CreateIndex(name: "IX_EvalGraders_EvalConfigId", schema: "SharpOMatic", table: "EvalGraders", column: "EvalConfigId");

            migrationBuilder.CreateIndex(name: "IX_EvalRows_EvalConfigId", schema: "SharpOMatic", table: "EvalRows", column: "EvalConfigId");

            migrationBuilder.CreateIndex(name: "IX_EvalRunGraderSummaries_EvalGraderId", schema: "SharpOMatic", table: "EvalRunGraderSummaries", column: "EvalGraderId");

            migrationBuilder.CreateIndex(name: "IX_EvalRunGraderSummaries_EvalRunId", schema: "SharpOMatic", table: "EvalRunGraderSummaries", column: "EvalRunId");

            migrationBuilder.CreateIndex(name: "IX_EvalRunRowGraders_EvalGraderId", schema: "SharpOMatic", table: "EvalRunRowGraders", column: "EvalGraderId");

            migrationBuilder.CreateIndex(name: "IX_EvalRunRowGraders_EvalRunRowId", schema: "SharpOMatic", table: "EvalRunRowGraders", column: "EvalRunRowId");

            migrationBuilder.CreateIndex(name: "IX_EvalRunRows_EvalRowId", schema: "SharpOMatic", table: "EvalRunRows", column: "EvalRowId");

            migrationBuilder.CreateIndex(name: "IX_EvalRunRows_EvalRunId", schema: "SharpOMatic", table: "EvalRunRows", column: "EvalRunId");

            migrationBuilder.CreateIndex(name: "IX_EvalRuns_EvalConfigId", schema: "SharpOMatic", table: "EvalRuns", column: "EvalConfigId");

            migrationBuilder.CreateIndex(name: "IX_Runs_WorkflowId_Created", schema: "SharpOMatic", table: "Runs", columns: new[] { "WorkflowId", "Created" });

            migrationBuilder.CreateIndex(name: "IX_Runs_WorkflowId_RunStatus", schema: "SharpOMatic", table: "Runs", columns: new[] { "WorkflowId", "RunStatus" });

            migrationBuilder.CreateIndex(name: "IX_Traces_RunId_Created", schema: "SharpOMatic", table: "Traces", columns: new[] { "RunId", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Assets", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "ConnectorConfigMetadata", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "ConnectorMetadata", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalData", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalRunGraderSummaries", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalRunRowGraders", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "ModelConfigMetadata", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "ModelMetadata", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "Settings", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "Traces", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalColumns", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalGraders", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalRunRows", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "Runs", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalRows", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalRuns", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "Workflows", schema: "SharpOMatic");

            migrationBuilder.DropTable(name: "EvalConfigs", schema: "SharpOMatic");
        }
    }
}
