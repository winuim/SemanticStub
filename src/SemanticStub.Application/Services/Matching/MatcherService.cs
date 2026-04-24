using System.Collections;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Models;

namespace SemanticStub.Application.Services;

/// <summary>
/// Evaluates <c>x-match</c> candidates and returns the most specific successful match without mutating request or stub state.
/// </summary>
public sealed class MatcherService
{
    private static readonly QueryMatchSpecificityComparer MatchSpecificityComparer = QueryMatchSpecificityComparer.Instance;
    private readonly FormBodyMatcher _formBodyMatcher;
    private readonly JsonBodyMatcher _jsonBodyMatcher;
    private readonly QueryValueMatcher _queryValueMatcher;
    private readonly RegexQueryMatcher _regexQueryMatcher;

    internal MatcherService(
        JsonBodyMatcher jsonBodyMatcher,
        FormBodyMatcher formBodyMatcher,
        QueryValueMatcher queryValueMatcher,
        RegexQueryMatcher regexQueryMatcher)
    {
        _jsonBodyMatcher = jsonBodyMatcher;
        _formBodyMatcher = formBodyMatcher;
        _queryValueMatcher = queryValueMatcher;
        _regexQueryMatcher = regexQueryMatcher;
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
            var queryMatched = IsQueryMatch(candidate, matchContext);
            var headerMatched = IsHeaderMatch(candidate.Headers, matchContext.Headers);
            var bodyMatched = IsBodyMatch(candidate.Body, matchContext);

            var mismatches = new List<MatchDimensionMismatch>();
            if (!queryMatched)
            {
                mismatches.AddRange(ComputeQueryMismatches(candidate, matchContext));
            }

            if (!headerMatched)
            {
                mismatches.AddRange(ComputeHeaderMismatches(candidate, matchContext.Headers));
            }

            evaluations.Add(new QueryMatchCandidateEvaluation
            {
                Candidate = candidate,
                QueryMatched = queryMatched,
                HeaderMatched = headerMatched,
                BodyMatched = bodyMatched,
                MismatchReasons = mismatches,
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
        return matchContext.RequestForm is not null && _formBodyMatcher.HasFormCondition(expectedBody)
            ? _formBodyMatcher.IsMatch(expectedBody, matchContext.RequestForm)
            : _jsonBodyMatcher.IsMatch(expectedBody, matchContext.RequestBody);
    }

    private bool IsQueryMatch(
        QueryMatchDefinition match,
        MatchEvaluationContext matchContext)
    {
        return _queryValueMatcher.IsExactMatch(match.Query, matchContext.Query, matchContext.QueryParameterTypes) &&
               _regexQueryMatcher.IsMatch(match.Query, matchContext.Query);
    }

    private MatchEvaluationContext CreateMatchContext(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var queryParameterTypes = QueryParameterTypeMapBuilder.Build(pathParameters, operation.Parameters);
        var bodyDocument = _jsonBodyMatcher.ParseRequestBody(body);
        var requestForm = _formBodyMatcher.ParseRequestBody(body, GetContentType(headers));
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

        return _queryValueMatcher.IsExactMatch(expected, actualValues, new Dictionary<string, string>(StringComparer.Ordinal)) &&
               _regexQueryMatcher.IsMatch(expected, actualValues);
    }

    private IReadOnlyList<MatchDimensionMismatch> ComputeQueryMismatches(
        QueryMatchDefinition candidate,
        MatchEvaluationContext matchContext)
    {
        return ComputeDimensionMismatches(
            "query",
            candidate.Query,
            matchContext.Query,
            matchContext.QueryParameterTypes);
    }

    private IReadOnlyList<MatchDimensionMismatch> ComputeHeaderMismatches(
        QueryMatchDefinition candidate,
        IReadOnlyDictionary<string, string> actualHeaders)
    {
        var actualValues = actualHeaders.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value),
            StringComparer.OrdinalIgnoreCase);

        return ComputeDimensionMismatches(
            "header",
            candidate.Headers,
            actualValues,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private IReadOnlyList<MatchDimensionMismatch> ComputeDimensionMismatches(
        string dimension,
        IReadOnlyDictionary<string, object?> expected,
        IReadOnlyDictionary<string, StringValues> actual,
        IReadOnlyDictionary<string, string> queryParameterTypes)
    {
        var mismatches = new List<MatchDimensionMismatch>();

        foreach (var pair in expected)
        {
            if (MatchOperatorDefinition.TryGetRegex(pair.Value, out var regexPattern))
            {
                var singleKey = new Dictionary<string, object?>(StringComparer.Ordinal) { [pair.Key] = pair.Value };
                var expectedPattern = ConvertExpectedValueToString(regexPattern);
                if (!actual.TryGetValue(pair.Key, out var actualValue))
                {
                    mismatches.Add(new MatchDimensionMismatch
                    {
                        Dimension = dimension,
                        Key = pair.Key,
                        Expected = expectedPattern,
                        Actual = null,
                        Kind = "missing",
                    });
                }
                else if (!_regexQueryMatcher.IsMatch(singleKey, actual))
                {
                    mismatches.Add(new MatchDimensionMismatch
                    {
                        Dimension = dimension,
                        Key = pair.Key,
                        Expected = expectedPattern,
                        Actual = actualValue.ToString(),
                        Kind = "unequal",
                    });
                }

                continue;
            }

            if (!MatchOperatorDefinition.TryGetEquals(pair.Value, out var expectedValue))
            {
                continue;
            }

            var singleExact = new Dictionary<string, object?>(StringComparer.Ordinal) { [pair.Key] = pair.Value };
            var expectedString = ConvertExpectedValueToString(expectedValue);
            if (!actual.TryGetValue(pair.Key, out var actualExact))
            {
                mismatches.Add(new MatchDimensionMismatch
                {
                    Dimension = dimension,
                    Key = pair.Key,
                    Expected = expectedString,
                    Actual = null,
                    Kind = "missing",
                });
            }
            else if (!_queryValueMatcher.IsExactMatch(singleExact, actual, queryParameterTypes))
            {
                mismatches.Add(new MatchDimensionMismatch
                {
                    Dimension = dimension,
                    Key = pair.Key,
                    Expected = expectedString,
                    Actual = actualExact.ToString(),
                    Kind = "unequal",
                });
            }
        }

        return mismatches;
    }

    private static string? ConvertExpectedValueToString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            IEnumerable sequence => string.Join(", ", sequence.Cast<object?>().Select(item => ConvertExpectedValueToString(item) ?? "null")),
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
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
            _bodyDocument = bodyDocument;
            RequestBody = bodyDocument?.RootElement;
            RequestForm = requestForm;
        }

        public IReadOnlyDictionary<string, StringValues> Query { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IReadOnlyDictionary<string, string> QueryParameterTypes { get; }

        public JsonElement? RequestBody { get; }

        public IReadOnlyDictionary<string, StringValues>? RequestForm { get; }

        private readonly JsonDocument? _bodyDocument;

        public void Dispose()
        {
            _bodyDocument?.Dispose();
        }
    }

}
