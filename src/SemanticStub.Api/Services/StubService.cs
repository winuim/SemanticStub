using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Collections;
using System.Globalization;

namespace SemanticStub.Api.Services;

/// <summary>
/// Converts a loaded <see cref="StubDocument"/> into concrete <see cref="StubResponse"/> values by applying deterministic path, method, query, header, and body matching rules.
/// </summary>
public sealed class StubService : IStubService
{
    private const string JsonContentType = "application/json";
    private static readonly string[] SupportedMethodOrder = [HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];
    private static readonly Func<string, string> MissingResponseFileReader = _ => throw new InvalidOperationException("No response file reader configured.");
    private readonly Func<StubDocument> documentAccessor;
    private readonly Func<string, string> responseFileReader;
    private readonly IMatcherService matcherService;
    private readonly ScenarioService scenarioService;
    private readonly ISemanticMatcherService? semanticMatcherService;
    private readonly ILogger<StubService>? logger;

    /// <summary>
    /// Creates a service that loads its stub document immediately from the configured loader and uses the supplied matcher implementation for conditional matches.
    /// </summary>
    /// <param name="loader">Provides the validated stub document and any relative file-backed response payloads.</param>
    /// <param name="matcherService">The matcher used to evaluate <c>x-match</c> candidates when a route and method have been resolved.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    /// <exception cref="DirectoryNotFoundException">Propagated when the loader cannot locate the configured definitions directory.</exception>
    /// <exception cref="FileNotFoundException">Propagated when the loader cannot find required stub files or response files.</exception>
    /// <exception cref="InvalidOperationException">Propagated when the loader cannot build a valid stub document.</exception>
    public StubService(IStubDefinitionLoader loader, IMatcherService matcherService, ScenarioService scenarioService)
        : this(CreateLoadedDocumentAccessor(loader), loader.LoadResponseFileContent, matcherService, scenarioService, semanticMatcherService: null, logger: null)
    {
    }

    /// <summary>
    /// Creates a service that always evaluates requests against the latest successfully loaded stub document.
    /// </summary>
    /// <param name="state">Provides the current validated stub document snapshot and file-backed response payloads.</param>
    /// <param name="matcherService">The matcher used to evaluate <c>x-match</c> candidates when a route and method have been resolved.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    internal StubService(
        StubDefinitionState state,
        IMatcherService matcherService,
        ScenarioService scenarioService,
        ISemanticMatcherService semanticMatcherService,
        ILogger<StubService> logger)
        : this(state.GetCurrentDocument, state.LoadResponseFileContent, matcherService, scenarioService, semanticMatcherService, logger)
    {
    }

    /// <summary>
    /// Creates a service over an already loaded stub document and disables relative file-backed responses.
    /// </summary>
    /// <param name="document">The validated stub document to evaluate.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    /// <remarks>Relative <c>x-response-file</c> responses will fail at runtime because no response-file reader is configured by this overload.</remarks>
    public StubService(StubDocument document, ScenarioService scenarioService)
        : this(document, MissingResponseFileReader, new MatcherService(), scenarioService, semanticMatcherService: null, logger: null)
    {
    }

    /// <summary>
    /// Creates a service over an already loaded stub document and uses the supplied delegate to resolve relative file-backed responses.
    /// </summary>
    /// <param name="document">The validated stub document to evaluate.</param>
    /// <param name="responseFileReader">Loads the contents of a relative response file selected by the matching stub.</param>
    /// <param name="matcherService">The matcher used to evaluate <c>x-match</c> candidates when a route and method have been resolved.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    public StubService(StubDocument document, Func<string, string> responseFileReader, IMatcherService matcherService, ScenarioService scenarioService)
        : this(() => document, responseFileReader, matcherService, scenarioService, semanticMatcherService: null, logger: null)
    {
    }

    internal StubService(
        StubDocument document,
        Func<string, string> responseFileReader,
        IMatcherService matcherService,
        ScenarioService scenarioService,
        ISemanticMatcherService? semanticMatcherService,
        ILogger<StubService>? logger = null)
        : this(() => document, responseFileReader, matcherService, scenarioService, semanticMatcherService, logger)
    {
    }

    private StubService(
        Func<StubDocument> documentAccessor,
        Func<string, string> responseFileReader,
        IMatcherService matcherService,
        ScenarioService scenarioService,
        ISemanticMatcherService? semanticMatcherService,
        ILogger<StubService>? logger)
    {
        this.documentAccessor = documentAccessor;
        this.responseFileReader = responseFileReader;
        this.matcherService = matcherService;
        this.scenarioService = scenarioService;
        this.semanticMatcherService = semanticMatcherService;
        this.logger = logger;
    }

    private static Func<StubDocument> CreateLoadedDocumentAccessor(IStubDefinitionLoader loader)
    {
        var document = loader.LoadDefaultDefinition();
        return () => document;
    }

    /// <summary>
    /// Resolves a response for callers that only need method and path matching.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with query, header, and body matching treated as unspecified.</returns>
    public StubMatchResult TryGetResponse(string method, string path, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Returns the configured HTTP methods for the supplied path in a stable order suitable for the <c>Allow</c> response header.
    /// </summary>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <returns>The configured methods for the resolved path, or an empty list when no path matches.</returns>
    public IReadOnlyList<string> GetAllowedMethods(string path)
    {
        var pathItem = ResolvePathItem(documentAccessor(), path);

        if (pathItem is null)
        {
            return Array.Empty<string>();
        }

        return SupportedMethodOrder
            .Where(method => GetOperation(method, pathItem) is not null)
            .ToArray();
    }

    /// <summary>
    /// Resolves a response while considering query-based match conditions so more specific stubs can override broad defaults.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Single-value query parameters keyed by parameter name. Missing or empty dictionaries mean no query conditions are available to match.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted and the body treated as unspecified.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            ConvertQueryValues(query),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query-based match conditions so more specific stubs can override broad defaults.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted and the body treated as unspecified.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, StringValues> query, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query and body match conditions so structured request payloads can select a narrower stub.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Single-value query parameters keyed by parameter name.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means no body conditions can match.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, string? body, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            ConvertQueryValues(query),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query and body match conditions so structured request payloads can select a narrower stub.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means no body conditions can match.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, StringValues> query, string? body, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body,
            out response);
    }

    /// <summary>
    /// Resolves the most specific stub response by evaluating path, method, query, headers, and body through the same matching pipeline.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate. Unsupported methods produce <see cref="StubMatchResult.MethodNotAllowed"/> only after a path match is found.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>. Exact paths win before template paths.</param>
    /// <param name="query">Query parameters keyed by parameter name. Repeated values are matched in request order.</param>
    /// <param name="headers">Request headers keyed by header name. Supply a case-insensitive dictionary for HTTP semantics.</param>
    /// <param name="body">The request body used for JSON body matching. Invalid JSON does not throw here; it simply prevents structured body conditions from matching.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see cref="StubMatchResult.Matched"/> when a conditional or default response can be built,
    /// <see cref="StubMatchResult.PathNotFound"/> when no path matches,
    /// <see cref="StubMatchResult.MethodNotAllowed"/> when the path exists but the method does not,
    /// or <see cref="StubMatchResult.ResponseNotConfigured"/> when a route matches but the selected response is missing content or otherwise unusable.
    /// </returns>
    /// <remarks>When a response defines <c>x-scenario.next</c>, selecting that response advances the in-memory scenario state before returning. Relative file-backed responses are loaded through the configured response-file reader.</remarks>
    public StubMatchResult TryGetResponse(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        out StubResponse? response)
    {
        var (matchResult, r) = TryGetResponseAsync(method, path, query, headers, body).GetAwaiter().GetResult();
        response = r;
        return matchResult;
    }

    /// <inheritdoc/>
    public async Task<(StubMatchResult Result, StubResponse? Response)> TryGetResponseAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var document = documentAccessor();

        if (!TryResolveOperation(document, method, path, out var pathItem, out var operation, out var failedMatchResult))
        {
            return (failedMatchResult, null);
        }

        if (OperationUsesScenario(operation))
        {
            return await scenarioService.ExecuteLockedAsync(
                () => TryGetResponseCoreAsync(method, path, pathItem, operation, query, headers, body)).ConfigureAwait(false);
        }

        return await TryGetResponseCoreAsync(method, path, pathItem, operation, query, headers, body).ConfigureAwait(false);
    }

    private static bool OperationUsesScenario(OperationDefinition operation)
    {
        return operation.Matches.Any(match => match.Response.Scenario is not null) ||
               operation.Responses.Values.Any(response => response.Scenario is not null);
    }

    private async Task<(StubMatchResult MatchResult, StubResponse? Response)> TryGetResponseCoreAsync(
        string method,
        string path,
        PathItemDefinition pathItem,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var (conditionalResult, response) = await TryBuildMatchedConditionalResponseAsync(method, path, pathItem, operation, query, headers, body).ConfigureAwait(false);

        if (conditionalResult.HasValue)
        {
            return (conditionalResult.Value, response);
        }

        return TryBuildDefaultOperationResponse(operation, out response)
            ? (StubMatchResult.Matched, response)
            : (StubMatchResult.ResponseNotConfigured, null);
    }

    private static IReadOnlyDictionary<string, StringValues> ConvertQueryValues(IReadOnlyDictionary<string, string> query)
    {
        return query.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value),
            StringComparer.Ordinal);
    }

    private static PathItemDefinition? ResolvePathItem(StubDocument document, string requestPath)
    {
        // Keep deterministic routing: exact paths always win before template paths.
        if (document.Paths.TryGetValue(requestPath, out var exactPathItem))
        {
            return exactPathItem;
        }

        return document.Paths
            .Where(entry => IsTemplateMatch(entry.Key, requestPath))
            .OrderByDescending(entry => GetTemplateSpecificity(entry.Key))
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Value)
            .FirstOrDefault();
    }

    private static bool IsTemplateMatch(string templatePath, string requestPath)
    {
        var templateSegments = GetPathSegments(templatePath);
        var requestSegments = GetPathSegments(requestPath);

        if (templateSegments.Length != requestSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var templateSegment = templateSegments[index];
            var requestSegment = requestSegments[index];

            if (IsPathParameterSegment(templateSegment))
            {
                if (string.IsNullOrEmpty(requestSegment))
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(templateSegment, requestSegment, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetTemplateSpecificity(string templatePath)
    {
        return GetPathSegments(templatePath).Count(segment => !IsPathParameterSegment(segment));
    }

    private static string[] GetPathSegments(string path)
    {
        return path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsPathParameterSegment(string segment)
    {
        return segment.Length > 2 &&
               segment[0] == '{' &&
               segment[^1] == '}' &&
               !segment[1..^1].Contains('{') &&
               !segment[1..^1].Contains('}');
    }

    private static OperationDefinition? GetOperation(string method, PathItemDefinition pathItem)
    {
        if (HttpMethods.IsGet(method))
        {
            return pathItem.Get;
        }

        if (HttpMethods.IsPost(method))
        {
            return pathItem.Post;
        }

        if (HttpMethods.IsPut(method))
        {
            return pathItem.Put;
        }

        if (HttpMethods.IsPatch(method))
        {
            return pathItem.Patch;
        }

        if (HttpMethods.IsDelete(method))
        {
            return pathItem.Delete;
        }

        return null;
    }

    private QueryMatchEvaluationResult TryBuildMatchedQueryResponse(
        string method,
        string path,
        PathItemDefinition pathItem,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        out StubResponse response)
    {
        response = null!;

        if (operation.Matches.Count == 0)
        {
            return QueryMatchEvaluationResult.NoMatch;
        }

        // Query/header/body conditions are combined, then the most specific surviving candidate wins.
        var matchedCandidate = matcherService.FindBestMatch(
            pathItem.Parameters,
            operation,
            query,
            headers,
            body,
            candidate => scenarioService.IsMatch(candidate.Response.Scenario) && IsDeterministicCandidate(candidate));

        if (matchedCandidate is null)
        {
            return QueryMatchEvaluationResult.NoMatch;
        }

        if (!TryBuildStubResponse(matchedCandidate.Response, out response))
        {
            return QueryMatchEvaluationResult.MatchedButInvalidResponse;
        }

        logger?.LogInformation(
            "Deterministic conditional match selected for '{Path}' {Method}. QueryKeys={QueryKeys}, HeaderKeys={HeaderKeys}, HasBody={HasBody}.",
            path,
            method.ToUpperInvariant(),
            matchedCandidate.Query.Count + matchedCandidate.PartialQuery.Count + matchedCandidate.RegexQuery.Count,
            matchedCandidate.Headers.Count,
            matchedCandidate.Body is not null);

        scenarioService.Advance(matchedCandidate.Response.Scenario);

        return QueryMatchEvaluationResult.Matched;
    }

    private static bool IsDeterministicCandidate(QueryMatchDefinition candidate)
    {
        return candidate.Query.Count > 0 ||
               candidate.PartialQuery.Count > 0 ||
               candidate.RegexQuery.Count > 0 ||
               candidate.Headers.Count > 0 ||
               candidate.Body is not null;
    }

    private bool TryResolveOperation(
        StubDocument document,
        string method,
        string path,
        out PathItemDefinition pathItem,
        out OperationDefinition operation,
        out StubMatchResult failedMatchResult)
    {
        pathItem = null!;
        operation = null!;
        failedMatchResult = StubMatchResult.Matched;

        var resolvedPathItem = ResolvePathItem(document, path);

        if (resolvedPathItem is null)
        {
            failedMatchResult = StubMatchResult.PathNotFound;
            return false;
        }

        var resolvedOperation = GetOperation(method, resolvedPathItem);

        if (resolvedOperation is null)
        {
            failedMatchResult = StubMatchResult.MethodNotAllowed;
            return false;
        }

        pathItem = resolvedPathItem;
        operation = resolvedOperation;
        return true;
    }

    private async Task<(StubMatchResult? Result, StubResponse Response)> TryBuildMatchedConditionalResponseAsync(
        string method,
        string path,
        PathItemDefinition pathItem,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var queryMatchResult = TryBuildMatchedQueryResponse(method, path, pathItem, operation, query, headers, body, out var response);

        if (queryMatchResult == QueryMatchEvaluationResult.Matched)
        {
            return (StubMatchResult.Matched, response);
        }

        if (queryMatchResult == QueryMatchEvaluationResult.MatchedButInvalidResponse)
        {
            return (StubMatchResult.ResponseNotConfigured, null!);
        }

        var (semanticMatchResult, semanticResponse) = await TryBuildSemanticMatchedResponseAsync(method, path, operation, query, headers, body).ConfigureAwait(false);

        if (semanticMatchResult == QueryMatchEvaluationResult.Matched)
        {
            logger?.LogInformation(
                "Semantic fallback produced a match for '{Path}' {Method}.",
                path,
                method.ToUpperInvariant());
            return (StubMatchResult.Matched, semanticResponse);
        }

        if (semanticMatchResult == QueryMatchEvaluationResult.MatchedButInvalidResponse)
        {
            return (StubMatchResult.ResponseNotConfigured, null!);
        }

        return (null, null!);
    }

    private async Task<(QueryMatchEvaluationResult Result, StubResponse Response)> TryBuildSemanticMatchedResponseAsync(
        string method,
        string path,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        if (semanticMatcherService is null)
        {
            return (QueryMatchEvaluationResult.NoMatch, null!);
        }

        if (operation.Matches.Count == 0)
        {
            return (QueryMatchEvaluationResult.NoMatch, null!);
        }

        logger?.LogInformation(
            "Deterministic conditional match not found for '{Path}' {Method}. Trying semantic fallback.",
            path,
            method.ToUpperInvariant());

        var matchedCandidate = await semanticMatcherService.FindBestMatchAsync(
            method,
            path,
            query,
            headers,
            body,
            operation.Matches,
            candidate => scenarioService.IsMatch(candidate.Response.Scenario)).ConfigureAwait(false);

        if (matchedCandidate is null)
        {
            logger?.LogDebug(
                "Semantic fallback did not produce a match for '{Path}' {Method}.",
                path,
                method.ToUpperInvariant());
            return (QueryMatchEvaluationResult.NoMatch, null!);
        }

        if (!TryBuildStubResponse(matchedCandidate.Response, out var response))
        {
            return (QueryMatchEvaluationResult.MatchedButInvalidResponse, null!);
        }

        scenarioService.Advance(matchedCandidate.Response.Scenario);

        return (QueryMatchEvaluationResult.Matched, response);
    }

    private bool TryBuildDefaultOperationResponse(OperationDefinition operation, out StubResponse response)
    {
        response = null!;

        var matchedResponse = operation.Responses
            .FirstOrDefault(entry => IsEligibleDefaultResponse(entry.Key, entry.Value));

        if (string.IsNullOrEmpty(matchedResponse.Key) || !int.TryParse(matchedResponse.Key, out var statusCode))
        {
            return false;
        }

        var built = TryBuildStubResponse(statusCode, matchedResponse.Value, out response);

        if (built)
        {
            scenarioService.Advance(matchedResponse.Value.Scenario);
        }

        return built;
    }

    private bool IsEligibleDefaultResponse(string statusCode, ResponseDefinition responseDefinition)
    {
        return int.TryParse(statusCode, out _) &&
               scenarioService.IsMatch(responseDefinition.Scenario) &&
               (responseDefinition.Content.Count > 0 || !string.IsNullOrEmpty(responseDefinition.ResponseFile));
    }

    private bool TryBuildStubResponse(int statusCode, ResponseDefinition responseDefinition, out StubResponse response)
    {
        response = null!;

        if (!string.IsNullOrEmpty(responseDefinition.ResponseFile))
        {
            response = CreateStubResponse(
                statusCode,
                responseDefinition.DelayMilliseconds,
                responseDefinition.Content,
                responseDefinition.Headers,
                responseDefinition.ResponseFile);

            return true;
        }

        var responseBody = BuildResponseBody(responseDefinition.Content);

        if (responseBody is null)
        {
            return false;
        }

        response = CreateStubResponse(
            statusCode,
            responseDefinition.DelayMilliseconds,
            responseDefinition.Content,
            responseDefinition.Headers,
            responseBody,
            filePath: null);

        return true;
    }

    private bool TryBuildStubResponse(QueryMatchResponseDefinition responseDefinition, out StubResponse response)
    {
        response = null!;

        if (responseDefinition.StatusCode <= 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(responseDefinition.ResponseFile))
        {
            response = CreateStubResponse(
                responseDefinition.StatusCode,
                responseDefinition.DelayMilliseconds,
                responseDefinition.Content,
                responseDefinition.Headers,
                responseDefinition.ResponseFile);

            return true;
        }

        var responseBody = BuildResponseBody(responseDefinition.Content);

        if (responseBody is null)
        {
            return false;
        }

        response = CreateStubResponse(
            responseDefinition.StatusCode,
            responseDefinition.DelayMilliseconds,
            responseDefinition.Content,
            responseDefinition.Headers,
            responseBody,
            filePath: null);

        return true;
    }

    private static StubResponse CreateStubResponse(
        int statusCode,
        int? delayMilliseconds,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        IReadOnlyDictionary<string, HeaderDefinition> headers,
        string responseBody,
        string? filePath)
    {
        return new StubResponse
        {
            StatusCode = statusCode,
            DelayMilliseconds = delayMilliseconds,
            ContentType = ResolveContentType(content),
            Headers = BuildResponseHeaders(headers),
            Body = responseBody,
            FilePath = filePath
        };
    }

    private StubResponse CreateStubResponse(
        int statusCode,
        int? delayMilliseconds,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        IReadOnlyDictionary<string, HeaderDefinition> headers,
        string responseFile)
    {
        if (Path.IsPathRooted(responseFile))
        {
            return CreateStubResponse(
                statusCode,
                delayMilliseconds,
                content,
                headers,
                string.Empty,
                responseFile);
        }

        return CreateStubResponse(
            statusCode,
            delayMilliseconds,
            content,
            headers,
            responseFileReader(responseFile),
            filePath: null);
    }

    private string? BuildResponseBody(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        var selectedKey = SelectMediaTypeKey(content);

        if (selectedKey is null || !content.TryGetValue(selectedKey, out var mediaType) || mediaType.Example is null)
        {
            return null;
        }

        // Non-JSON string examples are returned as-is; JSON types go through serialization.
        if (!IsJsonContentType(selectedKey) && mediaType.Example is string rawExample)
        {
            return rawExample;
        }

        return StubExampleSerializer.Serialize(mediaType.Example);
    }

    private static string ResolveContentType(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        return SelectMediaTypeKey(content) ?? JsonContentType;
    }

    // Prefer JSON content types to preserve deterministic behavior for stubs that declare
    // multiple media types (e.g. application/json alongside text/plain for documentation).
    // Fall back to the first declared entry only when no JSON type is present.
    private static string? SelectMediaTypeKey(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        return content.Keys.FirstOrDefault(IsJsonContentType) ?? content.Keys.FirstOrDefault();
    }

    private static bool IsJsonContentType(string contentType)
    {
        return contentType.Equals(JsonContentType, StringComparison.OrdinalIgnoreCase)
            || contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, StringValues> BuildResponseHeaders(IReadOnlyDictionary<string, HeaderDefinition> headers)
    {
        if (headers.Count == 0)
        {
            return new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        }

        var resolvedHeaders = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var resolvedValue = ResolveHeaderValue(header.Value);

            if (resolvedValue.Count == 0)
            {
                continue;
            }

            resolvedHeaders[header.Key] = string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
                ? resolvedValue
                : new StringValues(string.Join(", ", resolvedValue.ToArray().Where(static value => value is not null)!));
        }

        return resolvedHeaders;
    }

    private static StringValues ResolveHeaderValue(HeaderDefinition header)
    {
        return ConvertHeaderValueToStringValues(header.Example).Count > 0
            ? ConvertHeaderValueToStringValues(header.Example)
            : ConvertHeaderValueToStringValues(header.Schema?.Example);
    }

    private static StringValues ConvertHeaderValueToStringValues(object? value)
    {
        return value switch
        {
            null => StringValues.Empty,
            string text => new StringValues(text),
            char character => new StringValues(character.ToString()),
            bool boolean => new StringValues(boolean ? "true" : "false"),
            DateTime dateTime => new StringValues(dateTime.ToString("O", CultureInfo.InvariantCulture)),
            DateTimeOffset dateTimeOffset => new StringValues(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)),
            DateOnly dateOnly => new StringValues(dateOnly.ToString("O", CultureInfo.InvariantCulture)),
            TimeOnly timeOnly => new StringValues(timeOnly.ToString("O", CultureInfo.InvariantCulture)),
            Guid guid => new StringValues(guid.ToString()),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => new StringValues(Convert.ToString(value, CultureInfo.InvariantCulture)),
            IFormattable formattable => new StringValues(formattable.ToString(format: null, CultureInfo.InvariantCulture)),
            IEnumerable sequence => ConvertHeaderSequenceToStringValues(sequence),
            _ => new StringValues(value.ToString())
        };
    }

    private static StringValues ConvertHeaderSequenceToStringValues(IEnumerable sequence)
    {
        var values = sequence
            .Cast<object?>()
            .SelectMany(static value => ConvertHeaderValueToStringValues(value).ToArray())
            .Where(static value => !string.IsNullOrEmpty(value))
            .ToArray();

        return values.Length == 0 ? StringValues.Empty : new StringValues(values);
    }
}
