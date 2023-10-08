using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Fluss;

internal static class FlussActivitySource
{
    public static ActivitySource Source { get; } = new(GetName(), GetVersion());

    public static string GetName()
        => typeof(FlussActivitySource).Assembly.GetName().Name!;

    private static string GetVersion()
        => typeof(FlussActivitySource).Assembly.GetName().Version!.ToString();
}

public static class TracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddFlussInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddSource(FlussActivitySource.GetName());
        return builder;
    }
}
