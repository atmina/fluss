namespace Fluss.Events.TransientEvents;

/// An Event containing temporary information.
/// A TransientEvent will not be persisted.
public interface TransientEvent : Event { }

public record TransientEventEnvelope : EventEnvelope
{
    public DateTimeOffset ExpiresAt { get; init; }
}

public class ExpiresAfterAttribute : Attribute
{
    public double Ms { get; }
    public ExpiresAfterAttribute(double ms) => this.Ms = ms;
}
