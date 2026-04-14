using System.Text.Json;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Evaluates <c>x-match</c> candidates and returns the most specific successful match without mutating request or stub state.
/// </summary>
public sealed class MatcherService
{
    private static readonly QueryMatchSpecificityComparer MatchSpecificityComparer = QueryMatchSpecificityComparer.Instance;
    private readonly FormBodyMatcher formBodyMatcher;
    private readonly JsonBodyMatcher jsonBodyMatcher;
    private readonly QueryValueMatcher queryValueMatcher;
    private readonly RegexQueryMatcher regexQueryMatcher;

    internal MatcherService(
        JsonBodyMatcher jsonBodyMatcher,
        FormBodyMatcher formBodyMatcher,
        QueryValueMatcher queryValueMatcher,
        RegexQueryMatcher regexQueryMatcher)
    {
        this.jsonBodyMatcher = jsonBodyMatcher;
        this.formBodyMatcher = formBodyMatcher;
        this.queryValueMatcher = queryValueMatcher;
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
    /// Use this entry point when the caller needs the single best deterministic candidate for the current request.
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

    /// <summary>
    /// Evaluates each <c>x-match</c> candidate independently and reports which deterministic dimensions matched.
    /// </summary>
    /// <param name="pathParameters">Path-level parameters whose query-schema definitions may contribute typed comparison metadata.</param>
    /// <param name="operation">The operation whose <c>x-match</c> candidates should be evaluated.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="headers">Request headers keyed by header name. Supply a case-insensitive dictionary for HTTP semantics.</param>
    /// <param name="body">The request body used for JSON body matching. Invalid JSON is treated as "no structured body" instead of causing an exception.</param>
    /// <returns>The deterministic evaluation result for each configured candidate, in source order.</returns>
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
                HeaderMatched = IsHeaderMatch(candidate.Headers, matchContext.Headers),
                BodyMatched = IsBodyMatch(candidate.Body, matchContext),
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
               IsHeaderMatch(candidate.Headers, matchContext.Headers) &&
               IsBodyMatch(candidate.Body, matchContext);
    }

    private bool IsBodyMatch(object? expectedBody, MatchEvaluationContext matchContext)
    {
        return matchContext.RequestForm is not null && formBodyMatcher.HasFormCondition(expectedBody)
            ? formBodyMatcher.IsMatch(expectedBody, matchContext.RequestForm)
            : jsonBodyMatcher.IsMatch(expectedBody, matchContext.RequestBody);
    }

    private bool IsQueryMatch(
        QueryMatchDefinition match,
        MatchEvaluationContext matchContext)
    {
        return queryValueMatcher.IsExactMatch(match.Query, matchContext.Query, matchContext.QueryParameterTypes) &&
               regexQueryMatcher.IsMatch(match.Query, matchContext.Query);
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
        var requestForm = formBodyMatcher.ParseRequestBody(body, GetContentType(headers));
        return new MatchEvaluationContext(query, headers, queryParameterTypes, bodyDocument, requestForm);
    }

    private static string? GetContentType(IReadOnlyDictionary<string, string> headers)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }

    private bool IsHeaderMatch(IReadOnlyDictionary<string, object?> expected, IReadOnlyDictionary<string, string> actual)
    {
        var actualValues = actual.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value),
            StringComparer.OrdinalIgnoreCase);

        return queryValueMatcher.IsExactMatch(expected, actualValues, new Dictionary<string, string>(StringComparer.Ordinal)) &&
               regexQueryMatcher.IsMatch(expected, actualValues);
    }

    private readonly struct MatchEvaluationContext : IDisposable
    {
        public MatchEvaluationContext(
            IReadOnlyDictionary<string, StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            IReadOnlyDictionary<string, string> queryParameterTypes,
            JsonDocument? bodyDocument,
            IReadOnlyDictionary<string, StringValues>? requestForm)
        {
            Query = query;
            Headers = headers;
            QueryParameterTypes = queryParameterTypes;
            this.bodyDocument = bodyDocument;
            RequestBody = bodyDocument?.RootElement;
            RequestForm = requestForm;
        }

        public IReadOnlyDictionary<string, StringValues> Query { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IReadOnlyDictionary<string, string> QueryParameterTypes { get; }

        public JsonElement? RequestBody { get; }

        public IReadOnlyDictionary<string, StringValues>? RequestForm { get; }

        private readonly JsonDocument? bodyDocument;

        public void Dispose()
        {
            bodyDocument?.Dispose();
        }
    }

}
