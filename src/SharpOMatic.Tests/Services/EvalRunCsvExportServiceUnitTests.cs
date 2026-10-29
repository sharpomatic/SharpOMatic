namespace SharpOMatic.Tests.Services;

public sealed class EvalRunCsvExportServiceUnitTests
{
    [Fact]
    public async Task Export_writes_selected_columns_and_fields_in_order()
    {
        var setup = CreateSetup();
        var runRow = CreateRunRow(setup.Rows[1], 0, InputContext(("Name", "Alice"), ("Age", 42)), score: 0.5);
        var service = CreateService(setup, [runRow]);

        var stream = await service.ExportEvalRunCsvAsync(
            setup.EvalRunId,
            new EvalRunCsvExportOptions
            {
                ColumnIds = [setup.AgeColumn.EvalColumnId, setup.NameColumn.EvalColumnId],
                Fields = [EvalRunExportField.Score, EvalRunExportField.RowNumber, EvalRunExportField.Status],
            }
        );

        var lines = SplitLines(ReadCsv(stream));
        Assert.Equal(2, lines.Length);
        Assert.Equal("Row,Name,Age,Status,Score", lines[0]);
        Assert.Equal("2,Alice,42,Completed,0.5", lines[1]);
    }

    [Fact]
    public async Task Export_omits_columns_that_are_not_selected()
    {
        var setup = CreateSetup();
        var runRow = CreateRunRow(setup.Rows[0], 0, InputContext(("Name", "Alice"), ("Age", 42)));
        var service = CreateService(setup, [runRow]);

        var stream = await service.ExportEvalRunCsvAsync(setup.EvalRunId, new EvalRunCsvExportOptions { ColumnIds = [setup.NameColumn.EvalColumnId] });

        var lines = SplitLines(ReadCsv(stream));
        Assert.Equal("Name", lines[0]);
        Assert.Equal("Alice", lines[1]);
    }

    [Fact]
    public async Task Export_reads_column_value_from_input_path()
    {
        var setup = CreateSetup();
        setup.NameColumn.InputPath = "customer.name";
        var runRow = CreateRunRow(setup.Rows[0], 0, InputContext(("customer.name", "Alice")));
        var service = CreateService(setup, [runRow]);

        var stream = await service.ExportEvalRunCsvAsync(setup.EvalRunId, new EvalRunCsvExportOptions { ColumnIds = [setup.NameColumn.EvalColumnId] });

        var lines = SplitLines(ReadCsv(stream));
        Assert.Equal("Name", lines[0]);
        Assert.Equal("Alice", lines[1]);
    }

    [Fact]
    public async Task Export_writes_one_column_per_grader_per_path()
    {
        var setup = CreateSetup();
        var graderA = CreateGrader("Accuracy", 0, InputContext(("score", 0.75), ("feedback.reason", "too short, missing detail")));
        var graderB = CreateGrader("Tone", 1, InputContext(("score", 1)));
        var runRow = CreateRunRow(setup.Rows[0], 0, InputContext(("Name", "Alice")), graders: [graderA, graderB]);
        var service = CreateService(setup, [runRow]);

        var stream = await service.ExportEvalRunCsvAsync(setup.EvalRunId, new EvalRunCsvExportOptions { GraderPaths = ["score feedback.reason", "score"] });

        var lines = SplitLines(ReadCsv(stream));
        Assert.Equal("Accuracy.score,Accuracy.feedback.reason,Tone.score,Tone.feedback.reason", lines[0]);
        Assert.Equal("0.75,\"too short, missing detail\",1,", lines[1]);
    }

    [Fact]
    public async Task Export_leaves_grader_cells_blank_when_the_row_has_no_grader_output()
    {
        var setup = CreateSetup();
        var grader = CreateGrader("Accuracy", 0, InputContext(("score", 1)));
        var graded = CreateRunRow(setup.Rows[0], 0, InputContext(("Name", "Alice")), graders: [grader]);
        var failed = CreateRunRow(setup.Rows[1], 1, null, status: EvalRunStatus.Failed, error: "boom");
        var service = CreateService(setup, [graded, failed]);

        var stream = await service.ExportEvalRunCsvAsync(
            setup.EvalRunId,
            new EvalRunCsvExportOptions
            {
                ColumnIds = [setup.NameColumn.EvalColumnId],
                Fields = [EvalRunExportField.Error],
                GraderPaths = ["score"],
            }
        );

        var lines = SplitLines(ReadCsv(stream));
        Assert.Equal("Name,Error,Accuracy.score", lines[0]);
        Assert.Equal("Alice,,1", lines[1]);
        Assert.Equal(",boom,", lines[2]);
    }

    [Fact]
    public async Task Export_repeats_row_number_for_repeated_rows()
    {
        var setup = CreateSetup();
        var first = CreateRunRow(setup.Rows[1], 0, InputContext(("Name", "Alice")));
        var second = CreateRunRow(setup.Rows[1], 1, InputContext(("Name", "Alice")));
        var service = CreateService(setup, [first, second]);

        var stream = await service.ExportEvalRunCsvAsync(setup.EvalRunId, new EvalRunCsvExportOptions { Fields = [EvalRunExportField.RowNumber] });

        var lines = SplitLines(ReadCsv(stream));
        Assert.Equal(["Row", "2", "2"], lines);
    }

    [Fact]
    public async Task Export_returns_header_only_for_run_without_rows()
    {
        var setup = CreateSetup();
        var service = CreateService(setup, []);

        var stream = await service.ExportEvalRunCsvAsync(setup.EvalRunId, new EvalRunCsvExportOptions { ColumnIds = [setup.NameColumn.EvalColumnId] });

        var lines = SplitLines(ReadCsv(stream));
        Assert.Single(lines);
        Assert.Equal("Name", lines[0]);
    }

    private sealed record Setup(Guid EvalConfigId, Guid EvalRunId, EvalColumn NameColumn, EvalColumn AgeColumn, List<EvalRow> Rows);

    private static Setup CreateSetup()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = new EvalColumn
        {
            EvalColumnId = Guid.NewGuid(),
            EvalConfigId = evalConfigId,
            Name = "Name",
            Order = 0,
            EntryType = ContextEntryType.String,
            Optional = false,
            InputPath = null,
        };
        var ageColumn = new EvalColumn
        {
            EvalColumnId = Guid.NewGuid(),
            EvalConfigId = evalConfigId,
            Name = "Age",
            Order = 1,
            EntryType = ContextEntryType.Int,
            Optional = true,
            InputPath = null,
        };
        var rows = new List<EvalRow>
        {
            new()
            {
                EvalRowId = Guid.NewGuid(),
                EvalConfigId = evalConfigId,
                Order = 0,
            },
            new()
            {
                EvalRowId = Guid.NewGuid(),
                EvalConfigId = evalConfigId,
                Order = 1,
            },
        };

        return new Setup(evalConfigId, Guid.NewGuid(), nameColumn, ageColumn, rows);
    }

    private static TransferService CreateService(Setup setup, List<EvalRunRowDetail> runRows)
    {
        var repository = new Mock<IRepositoryService>();
        repository
            .Setup(service => service.GetEvalRunDetail(setup.EvalRunId))
            .ReturnsAsync(
                new EvalRunDetail
                {
                    EvalRun = new EvalRunSummary
                    {
                        EvalRunId = setup.EvalRunId,
                        EvalConfigId = setup.EvalConfigId,
                        Name = "Run",
                        Order = 0,
                        Started = DateTime.Now,
                        Status = EvalRunStatus.Completed,
                        TotalRows = runRows.Count,
                        CompletedRows = runRows.Count,
                        FailedRows = 0,
                        RunScoreMode = EvalRunScoreMode.AverageScore,
                    },
                    GraderSummaries = [],
                }
            );
        repository
            .Setup(service => service.GetEvalConfigDetail(setup.EvalConfigId))
            .ReturnsAsync(
                new EvalConfigDetail
                {
                    EvalConfig = new EvalConfig
                    {
                        EvalConfigId = setup.EvalConfigId,
                        WorkflowId = null,
                        Name = "Eval",
                        Description = "Eval",
                        MaxParallel = 1,
                    },
                    Graders = [],
                    Columns = [setup.NameColumn, setup.AgeColumn],
                    Rows = setup.Rows,
                    Data = [],
                }
            );
        repository.Setup(service => service.GetEvalRunRows(setup.EvalRunId, null, EvalRunRowSortField.Order, SortDirection.Ascending, 0, 0)).ReturnsAsync(runRows);

        return new TransferService(repository.Object, new Mock<IAssetStore>().Object);
    }

    private static EvalRunRowDetail CreateRunRow(
        EvalRow row,
        int order,
        string? inputContext,
        double? score = null,
        EvalRunStatus status = EvalRunStatus.Completed,
        string? error = null,
        List<EvalRunRowGraderDetail>? graders = null
    )
    {
        return new EvalRunRowDetail
        {
            EvalRunRowId = Guid.NewGuid(),
            EvalRunId = Guid.NewGuid(),
            EvalRowId = row.EvalRowId,
            Name = "Row",
            Order = order,
            Status = status,
            Score = score,
            Started = DateTime.Now,
            InputContext = inputContext,
            Error = error,
            Graders = graders ?? [],
        };
    }

    private static EvalRunRowGraderDetail CreateGrader(string label, int order, string outputContext)
    {
        return new EvalRunRowGraderDetail
        {
            EvalRunRowGraderId = Guid.NewGuid(),
            EvalRunRowId = Guid.NewGuid(),
            EvalGraderId = Guid.NewGuid(),
            Label = label,
            Order = order,
            Status = EvalRunStatus.Completed,
            Started = DateTime.Now,
            OutputContext = outputContext,
        };
    }

    private static string InputContext(params (string Path, object Value)[] values)
    {
        var context = new ContextObject();
        foreach (var (path, value) in values)
            context.Set(path, value);

        return context.Serialize();
    }

    private static string ReadCsv(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string[] SplitLines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}
