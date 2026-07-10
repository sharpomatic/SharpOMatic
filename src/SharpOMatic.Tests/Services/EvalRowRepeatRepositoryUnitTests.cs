namespace SharpOMatic.Tests.Services;

public sealed class EvalRowRepeatRepositoryUnitTests
{
    [Fact]
    public async Task UpsertEvalRows_defaults_null_repeat_to_one()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        var evalConfigId = Guid.NewGuid();
        await using (var context = CreateContext(options))
        {
            context.Database.EnsureCreated();
            context.EvalConfigs.Add(CreateEvalConfig(evalConfigId));
            await context.SaveChangesAsync();
        }

        var row = new EvalRow { EvalRowId = Guid.NewGuid(), EvalConfigId = evalConfigId, Order = 0, Repeat = null };
        var repository = new RepositoryService(new TestDbContextFactory(options));

        await repository.UpsertEvalRows([row]);

        await using var verifyContext = CreateContext(options);
        var savedRow = await verifyContext.EvalRows.AsNoTracking().SingleAsync();
        Assert.Equal(EvalRow.DefaultRepeat, savedRow.Repeat);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public async Task UpsertEvalRows_rejects_repeat_outside_allowed_range(int repeat)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        var repository = new RepositoryService(new TestDbContextFactory(options));

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            repository.UpsertEvalRows([new EvalRow { EvalRowId = Guid.NewGuid(), EvalConfigId = Guid.NewGuid(), Order = 0, Repeat = repeat }])
        );

        Assert.Contains("repeat must be between", exception.Message);
    }

    [Theory]
    [InlineData("Repeat", 1)]
    [InlineData("Name", 1)]
    public async Task UpsertEvalColumns_rejects_reserved_custom_column_names(string name, int order)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        var repository = new RepositoryService(new TestDbContextFactory(options));

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            repository.UpsertEvalColumns([CreateColumn(Guid.NewGuid(), name, order)])
        );

        Assert.Contains("reserved", exception.Message);
    }

    [Fact]
    public async Task GetEvalRunRows_appends_repeat_suffix_for_duplicate_source_rows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        var evalConfigId = Guid.NewGuid();
        var evalRunId = Guid.NewGuid();
        var evalRowId = Guid.NewGuid();
        var nameColumnId = Guid.NewGuid();
        await using (var context = CreateContext(options))
        {
            context.Database.EnsureCreated();
            context.EvalConfigs.Add(CreateEvalConfig(evalConfigId));
            context.EvalColumns.Add(new EvalColumn
            {
                EvalColumnId = nameColumnId,
                EvalConfigId = evalConfigId,
                Name = "Name",
                Order = 0,
                EntryType = ContextEntryType.String,
                Optional = false,
                InputPath = null,
            });
            context.EvalRows.Add(new EvalRow { EvalRowId = evalRowId, EvalConfigId = evalConfigId, Order = 0, Repeat = 3 });
            context.EvalData.Add(new EvalData { EvalDataId = Guid.NewGuid(), EvalRowId = evalRowId, EvalColumnId = nameColumnId, StringValue = "FRED" });
            context.EvalRuns.Add(CreateEvalRun(evalConfigId, evalRunId));
            context.EvalRunRows.AddRange(
                CreateEvalRunRow(evalRunId, evalRowId, 0),
                CreateEvalRunRow(evalRunId, evalRowId, 1),
                CreateEvalRunRow(evalRunId, evalRowId, 2)
            );
            await context.SaveChangesAsync();
        }

        var repository = new RepositoryService(new TestDbContextFactory(options));

        var rows = await repository.GetEvalRunRows(evalRunId, null, EvalRunRowSortField.Order, SortDirection.Ascending, 0, 0);

        Assert.Equal(["FRED (1)", "FRED (2)", "FRED (3)"], rows.Select(row => row.Name).ToArray());
    }

    private static SharpOMaticDbContext CreateContext(DbContextOptions<SharpOMaticDbContext> options)
    {
        return new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions()));
    }

    private static EvalConfig CreateEvalConfig(Guid evalConfigId) =>
        new()
        {
            EvalConfigId = evalConfigId,
            WorkflowId = null,
            Name = "Eval",
            Description = "Eval",
            MaxParallel = 1,
        };

    private static EvalRun CreateEvalRun(Guid evalConfigId, Guid evalRunId) =>
        new()
        {
            EvalRunId = evalRunId,
            EvalConfigId = evalConfigId,
            Name = "Run",
            Order = 1,
            Started = DateTime.UtcNow,
            Finished = DateTime.UtcNow,
            Status = EvalRunStatus.Completed,
            CancelRequested = false,
            TotalRows = 3,
            CompletedRows = 3,
            FailedRows = 0,
            RunScoreMode = EvalRunScoreMode.AverageScore,
        };

    private static EvalRunRow CreateEvalRunRow(Guid evalRunId, Guid evalRowId, int order) =>
        new()
        {
            EvalRunRowId = Guid.NewGuid(),
            EvalRunId = evalRunId,
            EvalRowId = evalRowId,
            Order = order,
            Started = DateTime.UtcNow,
            Finished = DateTime.UtcNow,
            Status = EvalRunStatus.Completed,
        };

    private static EvalColumn CreateColumn(Guid evalConfigId, string name, int order) =>
        new()
        {
            EvalColumnId = Guid.NewGuid(),
            EvalConfigId = evalConfigId,
            Name = name,
            Order = order,
            EntryType = ContextEntryType.String,
            Optional = true,
            InputPath = null,
        };

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext()
        {
            return new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions()));
        }
    }
}
