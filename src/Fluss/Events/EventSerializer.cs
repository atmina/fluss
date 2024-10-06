using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Fluss.Events;

public static class EventSerializer
{
    public class FooBar : JsonConverter<Event>
    {
        
        public override Event? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, Event value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
    
    public static JsonSerializerOptions Options { get; } = new()
    {
        Converters = { new FooBar() },
    };
    
    public static JsonObject Serialize(Event @event)
    {
        return JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(@event, Options))!;
    }
    
    public static Event Deserialize(JsonObject json)
    {
        return (Event)JsonSerializer.Deserialize(json.ToString(), typeof(Event))!;
    }
}