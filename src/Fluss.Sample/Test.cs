using Fluss.Regen;

namespace Fluss.Sample;

public class Test
{
    [Selector]
    public static int Add(int a, int b)
    {
        return a + b;
    }
}