using System.Collections.Immutable;
using Newtonsoft.Json.Linq;

namespace Fluss.Upcasting;

public interface IUpcaster
{
    public IEnumerable<JObject>? Upcast(JObject eventJson);
}

[AttributeUsage(AttributeTargets.Class)]
public class DependsOnAttribute(params Type[] upcasters) : Attribute
{
    public ImmutableHashSet<Type> Dependencies { get; private set; } = ImmutableHashSet<Type>.Empty.Union(upcasters);
}
