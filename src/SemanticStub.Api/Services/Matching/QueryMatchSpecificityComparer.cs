using System.Collections;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal sealed class QueryMatchSpecificityComparer : IComparer<QueryMatchDefinition>
{
    public static QueryMatchSpecificityComparer Instance { get; } = new();

    private QueryMatchSpecificityComparer()
    {
    }

    public int Compare(QueryMatchDefinition? x, QueryMatchDefinition? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var exactQueryComparison = y.Query.Count.CompareTo(x.Query.Count);

        if (exactQueryComparison != 0)
        {
            return exactQueryComparison;
        }

        var overallComparison = GetOverallSpecificity(y).CompareTo(GetOverallSpecificity(x));

        if (overallComparison != 0)
        {
            return overallComparison;
        }

        return y.RegexQuery.Count.CompareTo(x.RegexQuery.Count);
    }

    private static int GetOverallSpecificity(QueryMatchDefinition match)
    {
        return match.Query.Count + match.RegexQuery.Count + match.PartialQuery.Count + match.Headers.Count + GetBodySpecificity(match.Body);
    }

    private static int GetBodySpecificity(object? body)
    {
        // Nested body shapes should outrank shallower ones so more concrete matches win.
        return body switch
        {
            null => 0,
            IDictionary dictionary => dictionary.Count + dictionary.Values.Cast<object?>().Sum(GetBodySpecificity),
            IEnumerable list when body is not string => list.Cast<object?>().Sum(GetBodySpecificity),
            _ => 1
        };
    }
}
