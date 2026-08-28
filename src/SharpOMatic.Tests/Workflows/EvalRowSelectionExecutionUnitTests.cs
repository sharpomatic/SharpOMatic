namespace SharpOMatic.Tests.Workflows;

public sealed class EvalRowSelectionExecutionUnitTests
{
    [Fact]
    public void Explicit_row_selection_runs_only_the_selected_rows()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow>
        {
            CreateRow(evalConfigId, 0, 1),
            CreateRow(evalConfigId, 1, 1),
            CreateRow(evalConfigId, 2, 1),
        };

        var selectedRows = InvokeResolveEvalRowsForRun(rows, sampleCount: null, evalRowIds: [rows[1].EvalRowId]);

        Assert.Equal([rows[1].EvalRowId], selectedRows.Select(row => row.EvalRowId).ToArray());
    }

    [Fact]
    public void Explicit_row_selection_keeps_the_configured_repeat()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow>
        {
            CreateRow(evalConfigId, 0, 1),
            CreateRow(evalConfigId, 1, 3),
        };

        var selectedRows = InvokeResolveEvalRowsForRun(rows, sampleCount: null, evalRowIds: [rows[1].EvalRowId]);
        var workItems = InvokeBuildEvalRunWorkItems(selectedRows, isSampleRun: false);

        Assert.Equal(3, workItems.Count);
    }

    [Fact]
    public void Explicit_row_selection_cannot_be_combined_with_a_sample_count()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow> { CreateRow(evalConfigId, 0, 1) };

        var exception = Assert.Throws<SharpOMaticException>(() => InvokeResolveEvalRowsForRun(rows, sampleCount: 1, evalRowIds: [rows[0].EvalRowId]));

        Assert.Contains("Sample count cannot be combined with an explicit row selection.", exception.Message);
    }

    [Fact]
    public void Explicit_row_selection_rejects_unknown_row_ids()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow> { CreateRow(evalConfigId, 0, 1) };
        var missingRowId = Guid.NewGuid();

        var exception = Assert.Throws<SharpOMaticException>(() => InvokeResolveEvalRowsForRun(rows, sampleCount: null, evalRowIds: [missingRowId]));

        Assert.Contains(missingRowId.ToString(), exception.Message);
    }

    [Fact]
    public void Explicit_row_selection_rejects_rows_with_zero_repeat()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow> { CreateRow(evalConfigId, 0, 0) };

        var exception = Assert.Throws<SharpOMaticException>(() => InvokeResolveEvalRowsForRun(rows, sampleCount: null, evalRowIds: [rows[0].EvalRowId]));

        Assert.Contains("repeat of zero", exception.Message);
    }

    [Fact]
    public void Empty_row_selection_falls_back_to_running_every_row()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow>
        {
            CreateRow(evalConfigId, 0, 1),
            CreateRow(evalConfigId, 1, 1),
        };

        var selectedRows = InvokeResolveEvalRowsForRun(rows, sampleCount: null, evalRowIds: []);

        Assert.Equal(rows.Select(row => row.EvalRowId).ToArray(), selectedRows.Select(row => row.EvalRowId).ToArray());
    }

    private static List<EvalRow> InvokeResolveEvalRowsForRun(List<EvalRow> allRows, int? sampleCount, IReadOnlyList<Guid>? evalRowIds)
    {
        var method = typeof(EngineService).GetMethod("ResolveEvalRowsForRun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        try
        {
            var result = method.Invoke(null, [allRows, sampleCount, evalRowIds]);
            Assert.NotNull(result);
            return (List<EvalRow>)result;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static List<object> InvokeBuildEvalRunWorkItems(List<EvalRow> selectedRows, bool isSampleRun)
    {
        var method = typeof(EngineService).GetMethod("BuildEvalRunWorkItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var result = method.Invoke(null, [selectedRows, isSampleRun]);
        Assert.NotNull(result);
        return ((IEnumerable<object>)result).ToList();
    }

    private static EvalRow CreateRow(Guid evalConfigId, int order, int? repeat) =>
        new()
        {
            EvalRowId = Guid.NewGuid(),
            EvalConfigId = evalConfigId,
            Order = order,
            Repeat = repeat,
        };
}
