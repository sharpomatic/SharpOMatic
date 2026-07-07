using Microsoft.CodeAnalysis.Scripting;

namespace SharpOMatic.Tests.Services;

public sealed class ScriptCacheUnitTests
{
    [Fact]
    public void Same_code_and_globals_type_returns_the_same_cached_runner()
    {
        var service = new ScriptOptionsService([], []);

        var first = service.GetScriptRunner("1 + 1", typeof(object));
        var second = service.GetScriptRunner("1 + 1", typeof(object));

        // Same (code, globalsType) must compile once and hand back the identical runner instance.
        Assert.Same(first, second);
    }

    [Fact]
    public void Different_code_returns_different_runners()
    {
        var service = new ScriptOptionsService([], []);

        var first = service.GetScriptRunner("1 + 1", typeof(object));
        var second = service.GetScriptRunner("2 + 2", typeof(object));

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Same_code_with_different_globals_type_returns_different_runners()
    {
        var service = new ScriptOptionsService([], []);

        var first = service.GetScriptRunner("1 + 1", typeof(object));
        var second = service.GetScriptRunner("1 + 1", typeof(string));

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Cached_runner_returns_correct_value_and_is_reusable()
    {
        var service = new ScriptOptionsService([], []);

        var runner = service.GetScriptRunner("1 + 41", typeof(object));
        var firstResult = await runner(new object());
        var secondResult = await runner(new object());

        Assert.Equal(42, (int)firstResult!);
        Assert.Equal(42, (int)secondResult!);
    }

    [Fact]
    public async Task Cached_runner_reads_globals_fresh_on_each_invocation()
    {
        var service = new ScriptOptionsService([], []);

        var runner = service.GetScriptRunner("Value * 2", typeof(ScriptCacheTestGlobals));

        var firstResult = await runner(new ScriptCacheTestGlobals { Value = 3 });
        var secondResult = await runner(new ScriptCacheTestGlobals { Value = 10 });

        // The one cached runner is reused across executions with different globals (not baked to first call).
        Assert.Equal(6, (int)firstResult!);
        Assert.Equal(20, (int)secondResult!);
        Assert.Same(runner, service.GetScriptRunner("Value * 2", typeof(ScriptCacheTestGlobals)));
    }

    [Fact]
    public void Compilation_error_surfaces_as_CompilationErrorException()
    {
        var service = new ScriptOptionsService([], []);

        // Compilation happens inside GetScriptRunner, so callers keep catching CompilationErrorException as before.
        Assert.Throws<CompilationErrorException>(() => service.GetScriptRunner("this is not valid c#", typeof(object)));
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_code_share_a_single_runner()
    {
        var service = new ScriptOptionsService([], []);

        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() => service.GetScriptRunner("40 + 2", typeof(object)))).ToArray();
        var runners = await Task.WhenAll(tasks);

        // ExecutionAndPublication Lazy must compile once even under concurrency: all callers get the same runner.
        Assert.All(runners, runner => Assert.Same(runners[0], runner));
    }
}

public class ScriptCacheTestGlobals
{
    public int Value;
}
