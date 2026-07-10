namespace SharpOMatic.Tests.Workflows;

public sealed class EvalRowRepeatExecutionUnitTests
{
    [Fact]
    public void Full_eval_run_expands_rows_by_repeat_and_skips_zero()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow>
        {
            CreateRow(evalConfigId, 0, 0),
            CreateRow(evalConfigId, 1, 3),
            CreateRow(evalConfigId, 2, null),
        };

        var workItems = InvokeBuildEvalRunWorkItems(rows, isSampleRun: false);

        Assert.Equal(4, workItems.Count);
        Assert.Equal([0, 1, 2, 3], workItems.Select(GetWorkItemOrder).ToArray());
        Assert.Equal([rows[1].EvalRowId, rows[1].EvalRowId, rows[1].EvalRowId, rows[2].EvalRowId], workItems.Select(GetWorkItemRowId).ToArray());
    }

    [Fact]
    public void Sample_eval_run_executes_selected_rows_once_regardless_of_repeat()
    {
        var evalConfigId = Guid.NewGuid();
        var rows = new List<EvalRow>
        {
            CreateRow(evalConfigId, 0, 3),
            CreateRow(evalConfigId, 1, 2),
        };

        var workItems = InvokeBuildEvalRunWorkItems(rows, isSampleRun: true);

        Assert.Equal(2, workItems.Count);
        Assert.Equal([rows[0].EvalRowId, rows[1].EvalRowId], workItems.Select(GetWorkItemRowId).ToArray());
    }

    private static List<object> InvokeBuildEvalRunWorkItems(List<EvalRow> selectedRows, bool isSampleRun)
    {
        var method = typeof(EngineService).GetMethod("BuildEvalRunWorkItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var result = method.Invoke(null, [selectedRows, isSampleRun]);
        Assert.NotNull(result);
        return ((IEnumerable<object>)result).ToList();
    }

    private static int GetWorkItemOrder(object workItem)
    {
        var property = workItem.GetType().GetProperty("Order");
        Assert.NotNull(property);
        return (int)property.GetValue(workItem)!;
    }

    private static Guid GetWorkItemRowId(object workItem)
    {
        var property = workItem.GetType().GetProperty("Row");
        Assert.NotNull(property);
        var row = (EvalRow)property.GetValue(workItem)!;
        return row.EvalRowId;
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
