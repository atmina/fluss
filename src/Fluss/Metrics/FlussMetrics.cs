using System.Diagnostics.Metrics;

namespace Fluss.Metrics;

public class FlussMetrics
{
    public static readonly Meter Meter = new("fluss");
}