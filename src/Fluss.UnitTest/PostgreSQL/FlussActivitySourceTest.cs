using Moq;
using OpenTelemetry.Trace;
using Fluss.PostgreSQL;

namespace Fluss.UnitTest.PostgreSQL;

public class FlussPostgreSqlActivitySourceTest
{
    [Fact]
    public void Source_ShouldHaveCorrectNameAndVersion()
    {
        var source = ActivitySource.Source;

        Assert.NotNull(source);
        Assert.Equal(ActivitySource.GetName(), source.Name);
        Assert.Equal(typeof(ActivitySource).Assembly.GetName().Version!.ToString(), source.Version);
    }

    [Fact]
    public void GetName_ShouldReturnAssemblyName()
    {
        var name = ActivitySource.GetName();

        Assert.Equal(typeof(ActivitySource).Assembly.GetName().Name, name);
    }

    [Fact]
    public void AddFlussInstrumentation_ShouldAddFlussSource()
    {
        var mockBuilder = new Mock<TracerProviderBuilder>();
        mockBuilder.Setup(b => b.AddSource(It.IsAny<string>())).Returns(mockBuilder.Object);

        var result = Fluss.PostgreSQL.TracerProviderBuilderExtensions.AddPostgreSQLESInstrumentation(mockBuilder.Object);

        Assert.Same(mockBuilder.Object, result);
        mockBuilder.Verify(b => b.AddSource(ActivitySource.GetName()), Times.Once);
    }

    [Fact]
    public void AddFlussInstrumentation_ShouldThrowIfBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Fluss.PostgreSQL.TracerProviderBuilderExtensions.AddPostgreSQLESInstrumentation(null!));
    }
}
