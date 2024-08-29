using Fluss.Upcasting;
using Newtonsoft.Json.Linq;

namespace Fluss.UnitTest.Core.Upcasting;

public class UpcasterSorterTest
{
    private readonly UpcasterSorter _sorter = new();

    [Fact]
    public void SortsUpcastersBasedOnDependencies()
    {
        var up0 = new ExampleUpcasterNoDeps();
        var up1 = new ExampleUpcasterDeps1();
        var up2 = new ExampleUpcasterDeps2();
        var up3 = new ExampleUpcasterDeps3();

        var upcasters = new IUpcaster[] { up0, up3, up1, up2 };

        var sorted = _sorter.SortByDependencies(upcasters);

        Assert.Equal(sorted[0], up0);
        Assert.Equal(sorted[1], up1);
        Assert.Equal(sorted[2], up2);
        Assert.Equal(sorted[3], up3);
    }

    [Fact]
    public void ThrowsWhenCyclicDependencies()
    {
        var upcasters = new IUpcaster[] {
            new ExampleUpcasterNoDeps(),
            new ExampleUpcasterCyclic1(),
            new ExampleUpcasterCyclic2()
        };

        Assert.Throws<UpcasterSortException>(() => _sorter.SortByDependencies(upcasters));
    }

    [Fact]
    public void ThrowsWhenMissingDependencies()
    {
        var upcasters = new IUpcaster[] {
            new ExampleUpcasterDeps3(),
        };

        Assert.Throws<UpcasterSortException>(() => _sorter.SortByDependencies(upcasters));
    }
}

internal class ExampleUpcasterNoDeps : IUpcaster
{
    public IEnumerable<JObject> Upcast(JObject eventJson) => throw new NotImplementedException();
}

internal class ExampleUpcasterDeps1 : IUpcaster
{
    public IEnumerable<JObject> Upcast(JObject eventJson) => throw new NotImplementedException();
}

[DependsOn(typeof(ExampleUpcasterDeps1), typeof(ExampleUpcasterNoDeps))]
internal class ExampleUpcasterDeps2 : IUpcaster
{
    public IEnumerable<JObject> Upcast(JObject eventJson) => throw new NotImplementedException();
}

[DependsOn(typeof(ExampleUpcasterDeps1), typeof(ExampleUpcasterDeps2))]
internal class ExampleUpcasterDeps3 : IUpcaster
{
    public IEnumerable<JObject> Upcast(JObject eventJson) => throw new NotImplementedException();
}

[DependsOn(typeof(ExampleUpcasterCyclic2))]
internal class ExampleUpcasterCyclic1 : IUpcaster
{
    public IEnumerable<JObject> Upcast(JObject eventJson) => throw new NotImplementedException();
}

[DependsOn(typeof(ExampleUpcasterCyclic1))]
internal class ExampleUpcasterCyclic2 : IUpcaster
{
    public IEnumerable<JObject> Upcast(JObject eventJson) => throw new NotImplementedException();
}
