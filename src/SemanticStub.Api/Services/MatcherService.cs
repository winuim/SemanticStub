using System.Text.Json;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Evaluates <c>x-match</c> candidates and returns the most specific successful match without mutating request or stub state.
/// </summary>
public sealed class MatcherService : IMatcherService
{
    private static readonly QueryMatchSpecificityComparer MatchSpecificityComparer = QueryMatchSpecificityComparer.Instance;
    private readonly JsonBodyMatcher jsonBodyMatcher;
    private readonly QueryValueMatcher queryValueMatcher;
    private readonly RegexQueryMatcher regexQueryMatcher;

    /// <summary>
    /// Creates a matcher with no logging. Invalid regex patterns will silently produce non-matches.
    /// </summary>
    public MatcherService()
    {
        jsonBodyMatcher = new JsonBodyMatcher();
        queryValueMatcher = new QueryValueMatcher();
        regexQueryMatcher = new RegexQueryMatcher();
    }

    internal MatcherService(JsonBodyMatcher jsonBodyMatcher, RegexQueryMatcher regexQueryMatcher)
    {
        this.jsonBodyMatcher = jsonBodyMatcher;
        queryValueMatcher = new QueryValueMatcher();
        this.regexQueryMatcher = regexQueryMatcher;
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

        using var matchContext = CreateMatchContext(pathParameters, operation, query, headers, body);

        // Match conditions are conjunctive; after filtering, prefer the candidate with the most explicit constraints.
        return GetCandidatesMatchingRequest(
                operation.Matches,
                matchContext,
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
        using var matchContext = CreateMatchContext(pathParameters, operation, query, headers, body);
        var evaluations = new List<QueryMatchCandidateEvaluation>(operation.Matches.Count);

        foreach (var candidate in operation.Matches)
        {
            evaluations.Add(new QueryMatchCandidateEvaluation
            {
                Candidate = candidate,
                QueryMatched = IsQueryMatch(candidate, matchContext),
                HeaderMatched = IsExactHeaderMatch(candidate.Headers, matchContext.Headers),
                BodyMatched = jsonBodyMatcher.IsMatch(candidate.Body, matchContext.RequestBody),
            });
        }

        return evaluations;
    }

    private IEnumerable<QueryMatchDefinition> GetCandidatesMatchingRequest(
        IReadOnlyCollection<QueryMatchDefinition> candidates,
        MatchEvaluationContext matchContext,
        Func<QueryMatchDefinition, bool>? candidateFilter)
    {
        foreach (var candidate in candidates)
        {
            if (candidateFilter is not null && !candidateFilter(candidate))
            {
                continue;
            }

            if (IsCandidateMatch(candidate, matchContext))
            {
                yield return candidate;
            }
        }
    }

    private bool IsCandidateMatch(
        QueryMatchDefinition candidate,
        MatchEvaluationContext matchContext)
    {
        return IsQueryMatch(candidate, matchContext) &&
               IsExactHeaderMatch(candidate.Headers, matchContext.Headers) &&
               jsonBodyMatcher.IsMatch(candidate.Body, matchContext.RequestBody);
    }

    private bool IsQueryMatch(
        QueryMatchDefinition match,
        MatchEvaluationContext matchContext)
    {
        return queryValueMatcher.IsExactMatch(match.Query, matchContext.Query, matchContext.QueryParameterTypes) &&
               regexQueryMatcher.IsMatch(match.RegexQuery, matchContext.Query) &&
               queryValueMatcher.IsPartialMatch(match.PartialQuery, matchContext.Query, matchContext.QueryParameterTypes);
    }

    private MatchEvaluationContext CreateMatchContext(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var queryParameterTypes = QueryParameterTypeMapBuilder.Build(pathParameters, operation.Parameters);
        var bodyDocument = jsonBodyMatcher.ParseRequestBody(body);
        return new MatchEvaluationContext(query, headers, queryParameterTypes, bodyDocument);
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

    private readonly struct MatchEvaluationContext : IDisposable
    {
        public MatchEvaluationContext(
            IReadOnlyDictionary<string, StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            IReadOnlyDictionary<string, string> queryParameterTypes,
            JsonDocument? bodyDocument)
        {
            Query = query;
            Headers = headers;
            QueryParameterTypes = queryParameterTypes;
            this.bodyDocument = bodyDocument;
            RequestBody = bodyDocument?.RootElement;
        }

        public IReadOnlyDictionary<string, StringValues> Query { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IReadOnlyDictionary<string, string> QueryParameterTypes { get; }

        public JsonElement? RequestBody { get; }

        private readonly JsonDocument? bodyDocument;

        public void Dispose()
        {
            bodyDocument?.Dispose();
        }
    }

}
