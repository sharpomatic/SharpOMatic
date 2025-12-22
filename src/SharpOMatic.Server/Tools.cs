using SharpOMatic.Engine.Contexts;
using System.ComponentModel;

namespace SharpOMatic.Server;

public static class Tools
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
        var clockService = services.GetRequiredService<IClockService>();
        var context = services.GetRequiredService<ContextObject>();
        context.Set("GetTimeCalled", true);
        return DateTimeOffset.Now.ToString();
    }
}


public interface IClockService
{
    public string Now { get; }
}

public class ClockService : IClockService
{
    public string Now
    {
        get
        {
            return DateTimeOffset.Now.ToString();
        }
    }
}
