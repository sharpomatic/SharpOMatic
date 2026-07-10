namespace SharpOMatic.Tests.Services;

public sealed class EvalRowsCsvExportServiceUnitTests
{
    [Fact]
    public async Task Export_writes_header_and_data_rows_in_order()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var ageColumn = CreateColumn(evalConfigId, "Age", ContextEntryType.Int, false, 1);
        var scoreColumn = CreateColumn(evalConfigId, "Score", ContextEntryType.Double, false, 2);
        var flagColumn = CreateColumn(evalConfigId, "Flag", ContextEntryType.Bool, false, 3);
        var row1 = CreateRow(evalConfigId, 0);
        var row2 = CreateRow(evalConfigId, 1);
        var data = new List<EvalData>
        {
            CreateStringData(row1.EvalRowId, nameColumn.EvalColumnId, "Alice"),
            CreateIntData(row1.EvalRowId, ageColumn.EvalColumnId, 42),
            CreateDoubleData(row1.EvalRowId, scoreColumn.EvalColumnId, 3.5),
            CreateBoolData(row1.EvalRowId, flagColumn.EvalColumnId, true),
            CreateStringData(row2.EvalRowId, nameColumn.EvalColumnId, "Bob"),
            CreateIntData(row2.EvalRowId, ageColumn.EvalColumnId, 7),
            CreateDoubleData(row2.EvalRowId, scoreColumn.EvalColumnId, 1.25),
            CreateBoolData(row2.EvalRowId, flagColumn.EvalColumnId, false),
        };
        var repository = CreateRepository(evalConfigId, [nameColumn, ageColumn, scoreColumn, flagColumn], [row1, row2], data);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var stream = await service.ExportEvalRowsCsvAsync(evalConfigId);
        var csv = ReadCsv(stream);

        var lines = SplitLines(csv);
        Assert.Equal(3, lines.Length);
        Assert.Equal("Name,Age,Score,Flag,Repeat", lines[0]);
        Assert.Equal("Alice,42,3.5,true,1", lines[1]);
        Assert.Equal("Bob,7,1.25,false,1", lines[2]);
    }

    [Fact]
    public async Task Export_writes_row_repeat()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var row = CreateRow(evalConfigId, 0, repeat: 3);
        var data = new List<EvalData>
        {
            CreateStringData(row.EvalRowId, nameColumn.EvalColumnId, "Alice"),
        };
        var repository = CreateRepository(evalConfigId, [nameColumn], [row], data);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var stream = await service.ExportEvalRowsCsvAsync(evalConfigId);
        var csv = ReadCsv(stream);

        var lines = SplitLines(csv);
        Assert.Equal("Name,Repeat", lines[0]);
        Assert.Equal("Alice,3", lines[1]);
    }

    [Fact]
    public async Task Export_returns_header_only_for_empty_eval()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var repository = CreateRepository(evalConfigId, [nameColumn], [], []);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var stream = await service.ExportEvalRowsCsvAsync(evalConfigId);
        var csv = ReadCsv(stream);

        var lines = SplitLines(csv);
        Assert.Single(lines);
        Assert.Equal("Name,Repeat", lines[0]);
    }

    [Fact]
    public async Task Export_skips_asset_ref_and_asset_ref_list_columns()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var assetColumn = CreateColumn(evalConfigId, "Image", ContextEntryType.AssetRef, false, 1);
        var assetListColumn = CreateColumn(evalConfigId, "Attachments", ContextEntryType.AssetRefList, false, 2);
        var repository = CreateRepository(evalConfigId, [nameColumn, assetColumn, assetListColumn], [], []);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var stream = await service.ExportEvalRowsCsvAsync(evalConfigId);
        var csv = ReadCsv(stream);

        var lines = SplitLines(csv);
        Assert.Single(lines);
        Assert.Equal("Name,Repeat", lines[0]);
    }

    [Fact]
    public async Task Export_quotes_fields_containing_commas_and_quotes()
    {
        var evalConfigId = Guid.NewGuid();
        var notesColumn = CreateColumn(evalConfigId, "Notes", ContextEntryType.String, false, 0);
        var row = CreateRow(evalConfigId, 0);
        var data = new List<EvalData>
        {
            CreateStringData(row.EvalRowId, notesColumn.EvalColumnId, "Hello, \"world\""),
        };
        var repository = CreateRepository(evalConfigId, [notesColumn], [row], data);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var stream = await service.ExportEvalRowsCsvAsync(evalConfigId);
        var csv = ReadCsv(stream);

        var lines = SplitLines(csv);
        Assert.Equal(2, lines.Length);
        Assert.Equal("\"Hello, \"\"world\"\"\",1", lines[1]);
    }

    [Fact]
    public async Task Export_writes_empty_field_for_null_optional_column()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var ageColumn = CreateColumn(evalConfigId, "Age", ContextEntryType.Int, true, 1);
        var row = CreateRow(evalConfigId, 0);
        var data = new List<EvalData>
        {
            CreateStringData(row.EvalRowId, nameColumn.EvalColumnId, "Alice"),
        };
        var repository = CreateRepository(evalConfigId, [nameColumn, ageColumn], [row], data);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var stream = await service.ExportEvalRowsCsvAsync(evalConfigId);
        var csv = ReadCsv(stream);

        var lines = SplitLines(csv);
        Assert.Equal(2, lines.Length);
        Assert.Equal("Alice,,1", lines[1]);
    }

    [Fact]
    public async Task Export_produces_round_trip_compatible_csv()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var notesColumn = CreateColumn(evalConfigId, "Notes", ContextEntryType.String, true, 1);
        var row = CreateRow(evalConfigId, 0);
        var data = new List<EvalData>
        {
            CreateStringData(row.EvalRowId, nameColumn.EvalColumnId, "Alice, A."),
            CreateStringData(row.EvalRowId, notesColumn.EvalColumnId, "Said \"hello\""),
        };
        var repository = CreateRepository(evalConfigId, [nameColumn, notesColumn], [row], data);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var stream = await service.ExportEvalRowsCsvAsync(evalConfigId);
        var csv = ReadCsv(stream);
        var lines = SplitLines(csv);

        Assert.Equal(2, lines.Length);
        Assert.Equal("\"Alice, A.\",\"Said \"\"hello\"\"\",1", lines[1]);
    }

    private static Mock<IRepositoryService> CreateRepository(
        Guid evalConfigId,
        List<EvalColumn> columns,
        List<EvalRow> rows,
        List<EvalData> data
    )
    {
        var repository = new Mock<IRepositoryService>();
        repository
            .Setup(service => service.GetEvalConfigDetail(evalConfigId))
            .ReturnsAsync(
                new EvalConfigDetail
                {
                    EvalConfig = new EvalConfig
                    {
                        EvalConfigId = evalConfigId,
                        WorkflowId = null,
                        Name = "Eval",
                        Description = "Eval",
                        MaxParallel = 1,
                    },
                    Columns = columns,
                    Rows = rows,
                    Data = data,
                    Graders = [],
                }
            );

        return repository;
    }

    private static EvalColumn CreateColumn(
        Guid evalConfigId,
        string name,
        ContextEntryType entryType,
        bool optional,
        int order
    )
    {
        return new EvalColumn
        {
            EvalColumnId = Guid.NewGuid(),
            EvalConfigId = evalConfigId,
            Name = name,
            EntryType = entryType,
            Optional = optional,
            Order = order,
            InputPath = null,
        };
    }

    private static EvalRow CreateRow(Guid evalConfigId, int order, int? repeat = null)
    {
        return new EvalRow { EvalRowId = Guid.NewGuid(), EvalConfigId = evalConfigId, Order = order, Repeat = repeat ?? EvalRow.DefaultRepeat };
    }

    private static EvalData CreateStringData(Guid rowId, Guid columnId, string value) =>
        new() { EvalDataId = Guid.NewGuid(), EvalRowId = rowId, EvalColumnId = columnId, StringValue = value };

    private static EvalData CreateIntData(Guid rowId, Guid columnId, int value) =>
        new() { EvalDataId = Guid.NewGuid(), EvalRowId = rowId, EvalColumnId = columnId, IntValue = value };

    private static EvalData CreateDoubleData(Guid rowId, Guid columnId, double value) =>
        new() { EvalDataId = Guid.NewGuid(), EvalRowId = rowId, EvalColumnId = columnId, DoubleValue = value };

    private static EvalData CreateBoolData(Guid rowId, Guid columnId, bool value) =>
        new() { EvalDataId = Guid.NewGuid(), EvalRowId = rowId, EvalColumnId = columnId, BoolValue = value };

    private static string ReadCsv(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string[] SplitLines(string csv) =>
        csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}
