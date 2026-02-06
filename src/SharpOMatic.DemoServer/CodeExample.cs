namespace SharpOMatic.DemoServer;

public class CodeExample(int num)
{
    private readonly int _num = num;

    public int Doubled()
    {
        return _num * 2;
    }

    public static async Task<int> SlowDoubled(int num)
    {
        await Task.Delay(1000);
        return num * 2;
    }
}
