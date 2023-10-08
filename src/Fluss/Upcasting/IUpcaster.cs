using System.Collections.Immutable;
using Newtonsoft.Json.Linq;

namespace Fluss.Upcasting;

public interface IUpcaster
{
    public IEnumerable<JObject>? Upcast(JObject eventJson);
}

public class DependsOnAttribute : Attribute
{
    public ImmutableHashSet<Type> Dependencies { get; private set; }

    public DependsOnAttribute(params Type[] upcasters) =>
        Dependencies = ImmutableHashSet<Type>.Empty.Union(upcasters);
}
