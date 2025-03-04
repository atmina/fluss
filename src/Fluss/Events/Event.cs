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
        var serializer = new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
        };

        /*
         * Newtonsoft embeds additional type information when just using JObject.Parse(object), specifically that a given value is a Guid.
         *  This creates complications when using eventJson.Value<string>() in a later upcaster, because conversion is not possible anymore.
         *  To avoid this issue, we force a string representation and reparse it to get rid of this additional metadata because there is
         *  no option to avoid this information from being embedded otherwise.
         */
        var stringRepresentation = JsonConvert.SerializeObject(@event, serializer);

        return JObject.Parse(stringRepresentation);
    }
}
