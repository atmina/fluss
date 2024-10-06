using Fluss.Events;

namespace Fluss.UnitTest.Core.Events;

public class EventSerializerTest
{
    [Fact]
    public void Serialize_Deserialize_TestEvent1()
    {
        var testEvent = new TestEvent("Value");
        var serialized = EventSerializer.Serialize(testEvent);
        var deserialized = EventSerializer.Deserialize(serialized);

        Assert.Equal(testEvent, deserialized);
    }
    
    public record TestEvent(string Value) : Event;
}