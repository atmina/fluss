using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Fluss.Upcasting;

public interface IUpcaster
{
    public IEnumerable<JsonObject>? Upcast(JsonObject eventJson);
}

[AttributeUsage(AttributeTargets.Class)]
public class DependsOnAttribute(params Type[] upcasters) : Attribute
{
    public ImmutableHashSet<Type> Dependencies { get; private set; } = ImmutableHashSet<Type>.Empty.Union(upcasters);
}
