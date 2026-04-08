using System.Collections;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Evaluates <c>x-match</c> candidates and returns the most specific successful match without mutating request or stub state.
/// </summary>
public sealed class MatcherService : IMatcherService
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly QueryMatchSpecificityComparer MatchSpecificityComparer = QueryMatchSpecificityComparer.Instance;
    private readonly ILogger<MatcherService>? logger;
    private readonly JsonBodyMatcher jsonBodyMatcher;
    private readonly QueryValueMatcher queryValueMatcher;

    /// <summary>
    /// Creates a matcher with no logging. Invalid regex patterns will silently produce non-matches.
    /// </summary>
    public MatcherService()
    {
        jsonBodyMatcher = new JsonBodyMatcher();
        queryValueMatcher = new QueryValueMatcher();
    }

    internal MatcherService(JsonBodyMatcher jsonBodyMatcher, ILogger<MatcherService> logger)
    {
        this.logger = logger;
        this.jsonBodyMatcher = jsonBodyMatcher;
        queryValueMatcher = new QueryValueMatcher();
    }

    /// <summary>
    /// Evaluates conditional matches using operation-level query definitions and no request headers.
    /// </summary>
    /// <param name="operation">The operation whose <c>x-match</c> candidates should be evaluated.</param>
    /// <param name="query">Single-value query parameters keyed by parameter name.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means no structured body is available.</param>
    /// <returns>The best matching candidate, or <see langword="null"/> when no candidate matches.</returns>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        string? body)
    {
        return FindBestMatch(
            operation,
            ConvertQueryValues(query),
            body);
    }

    /// <summary>
    /// Evaluates conditional matches using operation-level query definitions and no request headers.
    /// </summary>
    /// <param name="operation">The operation whose <c>x-match</c> candidates should be evaluated.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means no structured body is available.</param>
    /// <returns>The best matching candidate, or <see langword="null"/> when no candidate matches.</returns>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        string? body)
    {
        return FindBestMatchWithOperationParameters(operation, query, EmptyHeaders, body);
    }

    /// <summary>
    /// Evaluates conditional matches using the operation's query parameter definitions and the supplied request headers.
    /// </summary>
    /// <param name="operation">The operation whose <c>x-match</c> candidates should be evaluated.</param>
    /// <param name="query">Single-value query parameters keyed by parameter name.</param>
    /// <param name="headers">Request headers keyed by header name. Supply a case-insensitive dictionary for HTTP semantics.</param>
    /// <param name="body">The request body used for JSON body matching. Invalid JSON is treated as "no structured body" instead of causing an exception.</param>
    /// <returns>The best matching conditional definition, or <see langword="null"/> when none satisfy the request.</returns>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        return FindBestMatch(
            operation,
            ConvertQueryValues(query),
            headers,
            body);
    }

    /// <summary>
    /// Evaluates conditional matches using the operation's query parameter definitions and the supplied request headers.
    /// </summary>
    /// <param name="operation">The operation whose <c>x-match</c> candidates should be evaluated.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="headers">Request headers keyed by header name. Supply a case-insensitive dictionary for HTTP semantics.</param>
    /// <param name="body">The request body used for JSON body matching. Invalid JSON is treated as "no structured body" instead of causing an exception.</param>
    /// <returns>The best matching conditional definition, or <see langword="null"/> when none satisfy the request.</returns>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        return FindBestMatchWithOperationParameters(operation, query, headers, body);
    }

    /// <summary>
    /// Filters candidates by every configured condition and returns the most specific surviving match.
    /// </summary>
    /// <param name="pathParameters">Path-level parameters whose query-schema definitions may contribute typed comparison metadata.</param>
    /// <param name="operation">The operation whose <c>x-match</c> candidates should be evaluated.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="headers">Request headers keyed by header name. Supply a case-insensitive dictionary for HTTP semantics.</param>
    /// <param name="body">The request body used for JSON body matching. Invalid JSON, invalid regex patterns, and regex timeouts are treated as non-matches instead of exceptions.</param>
    /// <returns>The best matching conditional definition, or <see langword="null"/> when none satisfy the request.</returns>
    /// <remarks>
    /// When multiple candidates match, exact-query specificity wins first, then overall query/header/body specificity,
    /// then regex-query specificity as the final tie-breaker.
    /// This is the most complete matcher entry point and is the contract used by <see cref="StubService"/>.
    /// </remarks>
    public QueryMatchDefinition? FindBestMatch(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        Func<QueryMatchDefinition, bool>? candidateFilter = null)
    {
        if (operation.Matches.Count == 0)
        {
            return null;
        }

        using var bodyDocument = jsonBodyMatcher.ParseRequestBody(body);
        var queryParameterTypes = QueryParameterTypeMapBuilder.Build(pathParameters, operation.Parameters);

        // Match conditions are conjunctive; after filtering, prefer the candidate with the most explicit constraints.
        return GetCandidatesMatchingRequest(
                operation.Matches,
                query,
                headers,
                queryParameterTypes,
                bodyDocument?.RootElement,
                candidateFilter)
            .OrderBy(match => match, MatchSpecificityComparer)
            .FirstOrDefault();
    }

    /// <inheritdoc/>
    public IReadOnlyList<QueryMatchCandidateEvaluation> EvaluateCandidates(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        using var bodyDocument = jsonBodyMatcher.ParseRequestBody(body);
        var queryParameterTypes = QueryParameterTypeMapBuilder.Build(pathParameters, operation.Parameters);
        var evaluations = new List<QueryMatchCandidateEvaluation>(operation.Matches.Count);

        foreach (var candidate in operation.Matches)
        {
            evaluations.Add(new QueryMatchCandidateEvaluation
            {
                Candidate = candidate,
                QueryMatched = IsQueryMatch(candidate, query, queryParameterTypes),
                HeaderMatched = IsExactHeaderMatch(candidate.Headers, headers),
                BodyMatched = jsonBodyMatcher.IsMatch(candidate.Body, bodyDocument?.RootElement),
            });
        }

        return evaluations;
    }

    private QueryMatchDefinition? FindBestMatchWithOperationParameters(
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        return FindBestMatch(
            operation.Parameters,
            operation,
            query,
            headers,
            body);
    }

    private IEnumerable<QueryMatchDefinition> GetCandidatesMatchingRequest(
        IReadOnlyCollection<QueryMatchDefinition> candidates,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyDictionary<string, string> queryParameterTypes,
        JsonElement? requestBody,
        Func<QueryMatchDefinition, bool>? candidateFilter)
    {
        foreach (var candidate in candidates)
        {
            if (candidateFilter is not null && !candidateFilter(candidate))
            {
                continue;
            }

            if (IsCandidateMatch(candidate, query, headers, queryParameterTypes, requestBody))
            {
                yield return candidate;
            }
        }
    }

    private bool IsCandidateMatch(
        QueryMatchDefinition candidate,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyDictionary<string, string> queryParameterTypes,
        JsonElement? requestBody)
    {
        return IsQueryMatch(candidate, query, queryParameterTypes) &&
               IsExactHeaderMatch(candidate.Headers, headers) &&
               jsonBodyMatcher.IsMatch(candidate.Body, requestBody);
    }

    private bool IsQueryMatch(
        QueryMatchDefinition match,
        IReadOnlyDictionary<string, StringValues> actual,
        IReadOnlyDictionary<string, string> queryParameterTypes)
    {
        return queryValueMatcher.IsExactMatch(match.Query, actual, queryParameterTypes) &&
               IsRegexQueryMatch(match.RegexQuery, actual) &&
               queryValueMatcher.IsPartialMatch(match.PartialQuery, actual, queryParameterTypes);
    }

    private bool IsRegexQueryMatch(
        IReadOnlyDictionary<string, object?> expected,
        IReadOnlyDictionary<string, StringValues> actual)
    {
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) || !IsRegexQueryValueMatch(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsRegexQueryValueMatch(object? expected, StringValues actual)
    {
        if (expected is IEnumerable expectedSequence && expected is not string)
        {
            return IsRegexQuerySequenceMatch(expectedSequence, actual);
        }

        return actual.Count == 1 &&
               actual[0] is not null &&
               IsSingleRegexQueryValueMatch(expected, actual[0]!);
    }

    private bool IsRegexQuerySequenceMatch(IEnumerable expectedSequence, StringValues actual)
    {
        var expectedValues = expectedSequence.Cast<object?>().ToArray();
        var actualValues = actual.ToArray();

        if (expectedValues.Length != actualValues.Length)
        {
            return false;
        }

        for (var index = 0; index < expectedValues.Length; index++)
        {
            if (actualValues[index] is null ||
                !IsSingleRegexQueryValueMatch(expectedValues[index], actualValues[index]!))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsSingleRegexQueryValueMatch(object? expected, string actual)
    {
        if (expected is not string pattern)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(actual, pattern, RegexOptions.CultureInvariant, RegexMatchTimeout);
        }
        catch (ArgumentException ex)
        {
            logger?.LogWarning(ex, "Invalid x-regex-query pattern '{Pattern}' in stub definition — treating as non-match.", pattern);
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            logger?.LogWarning("x-regex-query pattern '{Pattern}' timed out after {TimeoutMs}ms — treating as non-match.", pattern, RegexMatchTimeout.TotalMilliseconds);
            return false;
        }
    }

    private static IReadOnlyDictionary<string, StringValues> ConvertQueryValues(IReadOnlyDictionary<string, string> query)
    {
        return query.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value),
            StringComparer.Ordinal);
    }

    private static bool IsExactHeaderMatch(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

}
