using System.Reflection;

namespace Fluss.Upcasting;

public class UpcasterSortException() : Exception(
    $"Failed to sort Upcasters in {UpcasterSorter.MaxIterations} iterations. Ensure the following:\n" +
    " - There are no cyclic dependencies\n" +
    $" - All dependencies implement {nameof(Upcaster)}\n" +
    " - All upcasters are placed in the same Assembly");

public class UpcasterSorter
{
    internal const int MaxIterations = 100;

    public List<Upcaster> SortByDependencies(IEnumerable<Upcaster> upcasters)
    {
        var remaining = upcasters.ToHashSet();

        var result = remaining.Where(t => !GetDependencies(t).Any()).ToList();
        var includedTypes = result.Select(u => u.GetType()).ToHashSet();
        remaining.ExceptWith(result);

        var remainingIterations = MaxIterations;

        while (remaining.Count != 0)
        {
            // This approach is not the most performant admittedly but it works :)
            var next = remaining.Where(t => GetDependencies(t).All(d => includedTypes.Contains(d))).ToList();

            remaining.ExceptWith(next);

            result.AddRange(next);
            includedTypes.UnionWith(next.Select(u => u.GetType()));

            remainingIterations--;

            if (remainingIterations == 0)
            {
                throw new UpcasterSortException();
            }
        }

        return result;
    }

    private static IEnumerable<Type> GetDependencies(Upcaster upcaster)
    {
        var dependsOn = typeof(DependsOnAttribute);

        var attribute = upcaster.GetType().GetCustomAttribute(dependsOn);
        if (attribute is null) return new List<Type>();

        return dependsOn.GetProperty("Dependencies")?.GetValue(attribute) as IEnumerable<Type>
               ?? throw new ArgumentException("Could not find Dependencies property on DependsOn Attribute!");
    }
}
