namespace SharpOMatic.Tests.Services;

public sealed class EvalRowsCsvImportServiceUnitTests
{
    [Fact]
    public async Task Import_appends_valid_rows_and_ignores_extra_columns()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var ageColumn = CreateColumn(evalConfigId, "Age", ContextEntryType.Int, false, 1);
        var scoreColumn = CreateColumn(evalConfigId, "Score", ContextEntryType.Double, true, 2);
        var flagColumn = CreateColumn(evalConfigId, "Flag", ContextEntryType.Bool, false, 3);
        var repository = CreateRepository(
            evalConfigId,
            [nameColumn, ageColumn, scoreColumn, flagColumn],
            [new EvalRow { EvalRowId = Guid.NewGuid(), EvalConfigId = evalConfigId, Order = 4 }]
        );
        List<EvalRow>? insertedRows = null;
        List<EvalData>? insertedData = null;
        repository
            .Setup(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()))
            .Callback<List<EvalRow>, List<EvalData>>((rows, data) =>
            {
                insertedRows = rows;
                insertedData = data;
            })
            .Returns(Task.CompletedTask);

        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);
        var result = await service.ImportEvalRowsCsvAsync(
            evalConfigId,
            CsvStream("name,AGE,score,flag,extra\nAlice,42,3.5,true,ignored\nBob,7,,FALSE,ignored"),
            "rows.csv"
        );

        Assert.Equal(2, result.RowsImported);
        Assert.NotNull(insertedRows);
        Assert.NotNull(insertedData);
        Assert.Equal([5, 6], insertedRows.Select(row => row.Order).ToArray());
        Assert.All(insertedRows, row => Assert.Equal(evalConfigId, row.EvalConfigId));
        Assert.All(insertedRows, row => Assert.Equal(EvalRow.DefaultRepeat, row.Repeat));
        Assert.Contains(insertedData, data => data.EvalRowId == insertedRows[0].EvalRowId && data.EvalColumnId == nameColumn.EvalColumnId && data.StringValue == "Alice");
        Assert.Contains(insertedData, data => data.EvalRowId == insertedRows[0].EvalRowId && data.EvalColumnId == ageColumn.EvalColumnId && data.IntValue == 42);
        Assert.Contains(insertedData, data => data.EvalRowId == insertedRows[0].EvalRowId && data.EvalColumnId == scoreColumn.EvalColumnId && data.DoubleValue == 3.5);
        Assert.Contains(insertedData, data => data.EvalRowId == insertedRows[1].EvalRowId && data.EvalColumnId == flagColumn.EvalColumnId && data.BoolValue == false);
        Assert.DoesNotContain(insertedData, data => data.EvalRowId == insertedRows[1].EvalRowId && data.EvalColumnId == scoreColumn.EvalColumnId);
    }

    [Fact]
    public async Task Import_uses_optional_repeat_column_when_provided()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var repository = CreateRepository(evalConfigId, [nameColumn], []);
        List<EvalRow>? insertedRows = null;
        repository
            .Setup(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()))
            .Callback<List<EvalRow>, List<EvalData>>((rows, _) => insertedRows = rows)
            .Returns(Task.CompletedTask);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var result = await service.ImportEvalRowsCsvAsync(evalConfigId, CsvStream("Name,Repeat\nAlice,0\nBob,3\nCara,"), "rows.csv");

        Assert.Equal(3, result.RowsImported);
        Assert.NotNull(insertedRows);
        Assert.Equal([0, 3, 1], insertedRows.Select(row => row.Repeat.GetValueOrDefault()).ToArray());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("10001")]
    [InlineData("1.5")]
    public async Task Import_rejects_invalid_repeat_values_without_insert(string repeat)
    {
        var evalConfigId = Guid.NewGuid();
        var repository = CreateRepository(evalConfigId, [CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0)], []);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        await Assert.ThrowsAsync<SharpOMaticException>(() =>
            service.ImportEvalRowsCsvAsync(evalConfigId, CsvStream($"Name,Repeat\nAlice,{repeat}"), "rows.csv")
        );

        repository.Verify(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()), Times.Never);
    }

    [Fact]
    public async Task Import_rejects_missing_required_header_without_insert()
    {
        var evalConfigId = Guid.NewGuid();
        var repository = CreateRepository(
            evalConfigId,
            [
                CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0),
                CreateColumn(evalConfigId, "Age", ContextEntryType.Int, false, 1),
            ],
            []
        );
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            service.ImportEvalRowsCsvAsync(evalConfigId, CsvStream("Name\nAlice"), "rows.csv")
        );

        Assert.Contains("missing required column 'Age'", exception.Message);
        repository.Verify(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()), Times.Never);
    }

    [Fact]
    public async Task Import_rejects_missing_required_row_value_without_insert()
    {
        var evalConfigId = Guid.NewGuid();
        var repository = CreateRepository(
            evalConfigId,
            [
                CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0),
                CreateColumn(evalConfigId, "Age", ContextEntryType.Int, false, 1),
            ],
            []
        );
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            service.ImportEvalRowsCsvAsync(evalConfigId, CsvStream("Name,Age\nAlice,"), "rows.csv")
        );

        Assert.Contains("row 2", exception.Message);
        Assert.Contains("Age", exception.Message);
        repository.Verify(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()), Times.Never);
    }

    [Fact]
    public async Task Import_allows_empty_value_for_mandatory_string_column()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var notesColumn = CreateColumn(evalConfigId, "Notes", ContextEntryType.String, false, 1);
        var repository = CreateRepository(evalConfigId, [nameColumn, notesColumn], []);
        List<EvalData>? insertedData = null;
        repository
            .Setup(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()))
            .Callback<List<EvalRow>, List<EvalData>>((_, data) => insertedData = data)
            .Returns(Task.CompletedTask);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var result = await service.ImportEvalRowsCsvAsync(evalConfigId, CsvStream("Name,Notes\nAlice,"), "rows.csv");

        Assert.Equal(1, result.RowsImported);
        Assert.NotNull(insertedData);
        Assert.Contains(insertedData, data => data.EvalColumnId == notesColumn.EvalColumnId && data.StringValue == string.Empty);
    }

    [Theory]
    [InlineData(ContextEntryType.Int, "abc", "must be an integer")]
    [InlineData(ContextEntryType.Double, "abc", "must be a number")]
    [InlineData(ContextEntryType.Bool, "yes", "must be true or false")]
    public async Task Import_rejects_invalid_typed_values_without_insert(ContextEntryType entryType, string value, string expectedMessage)
    {
        var evalConfigId = Guid.NewGuid();
        var repository = CreateRepository(
            evalConfigId,
            [
                CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0),
                CreateColumn(evalConfigId, "Value", entryType, false, 1),
            ],
            []
        );
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            service.ImportEvalRowsCsvAsync(evalConfigId, CsvStream($"Name,Value\nAlice,{value}"), "rows.csv")
        );

        Assert.Contains(expectedMessage, exception.Message);
        repository.Verify(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()), Times.Never);
    }

    [Fact]
    public async Task Import_parses_quoted_commas_escaped_quotes_and_crlf()
    {
        var evalConfigId = Guid.NewGuid();
        var nameColumn = CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0);
        var notesColumn = CreateColumn(evalConfigId, "Notes", ContextEntryType.String, true, 1);
        var repository = CreateRepository(evalConfigId, [nameColumn, notesColumn], []);
        List<EvalData>? insertedData = null;
        repository
            .Setup(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()))
            .Callback<List<EvalRow>, List<EvalData>>((_, data) => insertedData = data)
            .Returns(Task.CompletedTask);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var result = await service.ImportEvalRowsCsvAsync(
            evalConfigId,
            CsvStream("Name,Notes\r\n\"Alice, A.\",\"Said \"\"hello\"\"\"\r\n"),
            "rows.csv"
        );

        Assert.Equal(1, result.RowsImported);
        Assert.NotNull(insertedData);
        Assert.Contains(insertedData, data => data.EvalColumnId == nameColumn.EvalColumnId && data.StringValue == "Alice, A.");
        Assert.Contains(insertedData, data => data.EvalColumnId == notesColumn.EvalColumnId && data.StringValue == "Said \"hello\"");
    }

    [Fact]
    public async Task Import_skips_blank_rows()
    {
        var evalConfigId = Guid.NewGuid();
        var repository = CreateRepository(
            evalConfigId,
            [CreateColumn(evalConfigId, "Name", ContextEntryType.String, false, 0)],
            []
        );
        List<EvalRow>? insertedRows = null;
        repository
            .Setup(service => service.InsertEvalRowsWithData(It.IsAny<List<EvalRow>>(), It.IsAny<List<EvalData>>()))
            .Callback<List<EvalRow>, List<EvalData>>((rows, _) => insertedRows = rows)
            .Returns(Task.CompletedTask);
        var service = new TransferService(repository.Object, new Mock<IAssetStore>().Object);

        var result = await service.ImportEvalRowsCsvAsync(evalConfigId, CsvStream("Name\n\nAlice\n , "), "rows.csv");

        Assert.Equal(1, result.RowsImported);
        Assert.NotNull(insertedRows);
        Assert.Single(insertedRows);
    }

    private static Mock<IRepositoryService> CreateRepository(
        Guid evalConfigId,
        List<EvalColumn> columns,
        List<EvalRow> rows
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
                    Data = [],
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

    private static MemoryStream CsvStream(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
    }
}
