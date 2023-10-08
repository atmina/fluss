using OpenTelemetry.Trace;

namespace Fluss.PostgreSQL;

internal class ActivitySource
{
    public static System.Diagnostics.ActivitySource Source { get; } = new(GetName(), GetVersion());

    public static string GetName()
        => typeof(ActivitySource).Assembly.GetName().Name!;

    private static string GetVersion()
        => typeof(ActivitySource).Assembly.GetName().Version!.ToString();
}

public static class TracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddPostgreSQLESInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddSource(ActivitySource.GetName());
        return builder;
    }
}
