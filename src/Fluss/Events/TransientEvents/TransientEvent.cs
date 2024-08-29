namespace Fluss.Events.TransientEvents;

/// An Event containing temporary information.
/// A TransientEvent will not be persisted.
public interface TransientEvent : Event;

public record TransientEventEnvelope : EventEnvelope
{
    public DateTimeOffset ExpiresAt { get; init; }
}

[AttributeUsage(AttributeTargets.Class)]
public class ExpiresAfterAttribute(double ms) : Attribute
{
    public double Ms { get; } = ms;
}
