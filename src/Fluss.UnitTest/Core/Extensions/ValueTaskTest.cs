using Fluss.Extensions;

namespace Fluss.UnitTest.Core.Extensions;

public class ValueTaskTest
{
    [Fact]
    public async void AnyReturnsFalse()
    {
        Assert.False(await new[] { False() }.AnyAsync());
    }

    [Fact]
    public async void AnyReturnsTrue()
    {
        Assert.True(await new[] { False(), True() }.AnyAsync());
    }

    [Fact]
    public async void AllReturnsFalse()
    {
        Assert.False(await new[] { False(), True() }.AllAsync());
    }

    [Fact]
    public async void AllReturnsTrue()
    {
        Assert.True(await new[] { True(), True() }.AllAsync());
    }

    [Fact]
    public void GetResultWorksForBoolean()
    {
        Assert.True(True().GetResult());
    }

    [Fact]
    public void GetResultWorksForEmpty()
    {
        Empty().GetResult();
    }

    private ValueTask<bool> True()
    {
        return ValueTask.FromResult(true);
    }

    private ValueTask<bool> False()
    {
        return ValueTask.FromResult(false);
    }

    private ValueTask Empty()
    {
        return ValueTask.CompletedTask;
    }
}
