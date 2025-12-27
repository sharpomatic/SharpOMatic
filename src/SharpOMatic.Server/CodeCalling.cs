namespace SharpOMatic.Server;

public class CodeCalling
{
    private readonly int _num;

    public CodeCalling(int num)
    {
        _num = num;
    }

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