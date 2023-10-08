using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Fluss.Events;

public interface Event
{
}

public static class EventExtension
{
    public static JObject ToJObject(this Event @event)
    {
        var serializer = new JsonSerializer { TypeNameHandling = TypeNameHandling.All, TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full };
        return JObject.FromObject(@event, serializer);
    }
}
