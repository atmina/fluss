using System.Collections.Immutable;
using Fluss.Events;
using Newtonsoft.Json.Linq;

namespace Fluss.Upcasting;

public abstract class Upcaster {
    public virtual IEnumerable<JObject>? Upcast(RawEventEnvelope rawEventEnvelope, IEnumerable<JObject> futureEventJsons) {
        return Upcast(rawEventEnvelope.RawEvent, futureEventJsons);
    }

    public virtual IEnumerable<JObject>? Upcast(JObject eventJson, IEnumerable<JObject> futureEventJsons) {
        return Upcast(eventJson);
    }

    public virtual IEnumerable<JObject>? Upcast(JObject eventJson) => null;
}

public class DependsOnAttribute : Attribute {
    public ImmutableHashSet<Type> Dependencies { get; private set; }

    public DependsOnAttribute(params Type[] upcasters) =>
        Dependencies = ImmutableHashSet<Type>.Empty.Union(upcasters);
}
