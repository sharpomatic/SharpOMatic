
#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _1002 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "SharpOMatic");

            migrationBuilder.CreateTable(
                name: "AssetFolders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetFolders", x => x.FolderId);
                });

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
                name: "EvalConfigs",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    MaxParallel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalConfigs", x => x.EvalConfigId);
                });

            migrationBuilder.CreateTable(
                name: "Informations",
                schema: "SharpOMatic",
                columns: table => new
                {
                    InformationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InformationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Informations", x => x.InformationId);
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
                name: "EvalColumns",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalColumnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryType = table.Column<int>(type: "INTEGER", nullable: false),
                    Optional = table.Column<bool>(type: "INTEGER", nullable: false),
                    InputPath = table.Column<string>(type: "TEXT", nullable: true)
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvalGraders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalGraderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    PassThreshold = table.Column<double>(type: "REAL", nullable: false)
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvalRows",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvalRuns",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Started = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Finished = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    CancelRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    AveragePassRate = table.Column<double>(type: "REAL", nullable: true)
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
                        onDelete: ReferentialAction.Cascade);
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
                name: "EvalData",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalColumnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StringValue = table.Column<string>(type: "TEXT", nullable: true),
                    IntValue = table.Column<int>(type: "INTEGER", nullable: true),
                    DoubleValue = table.Column<double>(type: "REAL", nullable: true),
                    BoolValue = table.Column<bool>(type: "INTEGER", nullable: true)
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvalData_EvalRows_EvalRowId",
                        column: x => x.EvalRowId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRows",
                        principalColumn: "EvalRowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvalRunGraderSummaries",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunGraderSummaryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalGraderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TotalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinScore = table.Column<double>(type: "REAL", nullable: true),
                    MaxScore = table.Column<double>(type: "REAL", nullable: true),
                    AverageScore = table.Column<double>(type: "REAL", nullable: true),
                    MedianScore = table.Column<double>(type: "REAL", nullable: true),
                    StandardDeviation = table.Column<double>(type: "REAL", nullable: true),
                    PassRate = table.Column<double>(type: "REAL", nullable: true)
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvalRunGraderSummaries_EvalRuns_EvalRunId",
                        column: x => x.EvalRunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRuns",
                        principalColumn: "EvalRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvalRunRows",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Started = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Finished = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    InputContext = table.Column<string>(type: "TEXT", nullable: true),
                    OutputContext = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvalRunRows_EvalRuns_EvalRunId",
                        column: x => x.EvalRunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRuns",
                        principalColumn: "EvalRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "SharpOMatic",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                        name: "FK_Assets_AssetFolders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "SharpOMatic",
                        principalTable: "AssetFolders",
                        principalColumn: "FolderId",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "EvalRunRowGraders",
                schema: "SharpOMatic",
                columns: table => new
                {
                    EvalRunRowGraderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalRunRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalGraderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvalRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Started = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Finished = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: true),
                    InputContext = table.Column<string>(type: "TEXT", nullable: true),
                    OutputContext = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvalRunRowGraders_EvalRunRows_EvalRunRowId",
                        column: x => x.EvalRunRowId,
                        principalSchema: "SharpOMatic",
                        principalTable: "EvalRunRows",
                        principalColumn: "EvalRunRowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetFolders_Name",
                schema: "SharpOMatic",
                table: "AssetFolders",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_FolderId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "FolderId");

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
                name: "IX_Assets_Scope_FolderId_Created",
                schema: "SharpOMatic",
                table: "Assets",
                columns: new[] { "Scope", "FolderId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_EvalColumns_EvalConfigId",
                schema: "SharpOMatic",
                table: "EvalColumns",
                column: "EvalConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalData_EvalColumnId",
                schema: "SharpOMatic",
                table: "EvalData",
                column: "EvalColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalData_EvalRowId",
                schema: "SharpOMatic",
                table: "EvalData",
                column: "EvalRowId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalGraders_EvalConfigId",
                schema: "SharpOMatic",
                table: "EvalGraders",
                column: "EvalConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRows_EvalConfigId",
                schema: "SharpOMatic",
                table: "EvalRows",
                column: "EvalConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRunGraderSummaries_EvalGraderId",
                schema: "SharpOMatic",
                table: "EvalRunGraderSummaries",
                column: "EvalGraderId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRunGraderSummaries_EvalRunId",
                schema: "SharpOMatic",
                table: "EvalRunGraderSummaries",
                column: "EvalRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRunRowGraders_EvalGraderId",
                schema: "SharpOMatic",
                table: "EvalRunRowGraders",
                column: "EvalGraderId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRunRowGraders_EvalRunRowId",
                schema: "SharpOMatic",
                table: "EvalRunRowGraders",
                column: "EvalRunRowId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRunRows_EvalRowId",
                schema: "SharpOMatic",
                table: "EvalRunRows",
                column: "EvalRowId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRunRows_EvalRunId",
                schema: "SharpOMatic",
                table: "EvalRunRows",
                column: "EvalRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRuns_EvalConfigId_Order",
                schema: "SharpOMatic",
                table: "EvalRuns",
                columns: new[] { "EvalConfigId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Informations_TraceId_Created",
                schema: "SharpOMatic",
                table: "Informations",
                columns: new[] { "TraceId", "Created" });

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
                name: "EvalData",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalRunGraderSummaries",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalRunRowGraders",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Informations",
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
                name: "AssetFolders",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalColumns",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalGraders",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalRunRows",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Runs",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalRows",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalRuns",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "Workflows",
                schema: "SharpOMatic");

            migrationBuilder.DropTable(
                name: "EvalConfigs",
                schema: "SharpOMatic");
        }
    }
}
