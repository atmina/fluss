using System.Text.Json.Nodes;

namespace Fluss.Events;

public abstract record Envelope
{
    public long Version { get; init; }

    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public Guid? By { get; init; }
}

public sealed record RawEventEnvelope : Envelope
{
    public required JsonObject RawEvent { get; init; }
}

public record EventEnvelope : Envelope
{
    public required Event Event { get; init; }
}
