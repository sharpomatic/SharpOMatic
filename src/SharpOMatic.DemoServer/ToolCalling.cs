namespace SharpOMatic.DemoServer;

public static class ToolCalling
{
    [Description("Get a friendly greeting.")]
    public static string GetGreeting(IServiceProvider services)
    {
        var context = services.GetRequiredService<ContextObject>();
        context.Set("GetGreetingCalled", true);
        return "Howdy doody!";
    }

    [Description("Get current time")]
    public static string GetTime(IServiceProvider services)
    {
        var context = services.GetRequiredService<ContextObject>();
        context.Set("GetTimeCalled", true);
        return DateTimeOffset.Now.ToString();
    }
}
