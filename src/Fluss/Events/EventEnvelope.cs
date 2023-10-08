using Newtonsoft.Json.Linq;

namespace Fluss.Events;

public abstract record Envelope
{
    public long Version { get; init; }

    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public Guid? By { get; init; }
}

public record RawEventEnvelope : Envelope
{
    public required JObject RawEvent { get; init; }
}

public record EventEnvelope : Envelope
{
    public required Event Event { get; init; }
}
