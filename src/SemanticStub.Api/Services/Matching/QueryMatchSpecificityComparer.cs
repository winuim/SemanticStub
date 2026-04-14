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

        var exactQueryComparison = GetEqualsSpecificity(y.Query).CompareTo(GetEqualsSpecificity(x.Query));

        if (exactQueryComparison != 0)
        {
            return exactQueryComparison;
        }

        var overallComparison = GetOverallSpecificity(y).CompareTo(GetOverallSpecificity(x));

        if (overallComparison != 0)
        {
            return overallComparison;
        }

        return GetRegexSpecificity(y.Query).CompareTo(GetRegexSpecificity(x.Query));
    }

    private static int GetOverallSpecificity(QueryMatchDefinition match)
    {
        return GetFieldSpecificity(match.Query) + GetFieldSpecificity(match.Headers) + GetBodySpecificity(match.Body);
    }

    private static int GetFieldSpecificity(IReadOnlyDictionary<string, object?> fields)
    {
        return GetEqualsSpecificity(fields) + GetRegexSpecificity(fields);
    }

    private static int GetEqualsSpecificity(IReadOnlyDictionary<string, object?> fields)
    {
        return fields.Count(field => MatchOperatorDefinition.TryGetEquals(field.Value, out _));
    }

    private static int GetRegexSpecificity(IReadOnlyDictionary<string, object?> fields)
    {
        return fields.Count(field => MatchOperatorDefinition.TryGetRegex(field.Value, out _));
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
