using Moq;
using OpenTelemetry.Trace;

namespace Fluss.UnitTest.Core;

public class FlussActivitySourceTest
{
    [Fact]
    public void Source_ShouldHaveCorrectNameAndVersion()
    {
        var source = FlussActivitySource.Source;

        Assert.NotNull(source);
        Assert.Equal(FlussActivitySource.GetName(), source.Name);
        Assert.Equal(typeof(FlussActivitySource).Assembly.GetName().Version!.ToString(), source.Version);
    }

    [Fact]
    public void GetName_ShouldReturnAssemblyName()
    {
        var name = FlussActivitySource.GetName();

        Assert.Equal(typeof(FlussActivitySource).Assembly.GetName().Name, name);
    }

    [Fact]
    public void AddFlussInstrumentation_ShouldAddFlussSource()
    {
        var mockBuilder = new Mock<TracerProviderBuilder>();
        mockBuilder.Setup(b => b.AddSource(It.IsAny<string>())).Returns(mockBuilder.Object);

        var result = TracerProviderBuilderExtensions.AddFlussInstrumentation(mockBuilder.Object);

        Assert.Same(mockBuilder.Object, result);
        mockBuilder.Verify(b => b.AddSource(FlussActivitySource.GetName()), Times.Once);
    }

    [Fact]
    public void AddFlussInstrumentation_ShouldThrowIfBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => TracerProviderBuilderExtensions.AddFlussInstrumentation(null!));
    }
}
