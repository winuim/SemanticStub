using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal sealed class StubDefaultResponseSelector
{
    private readonly StubResponseBuilder responseBuilder;
    private readonly ScenarioService scenarioService;

    public StubDefaultResponseSelector(
        StubResponseBuilder responseBuilder,
        ScenarioService scenarioService)
    {
        this.responseBuilder = responseBuilder;
        this.scenarioService = scenarioService;
    }

    public bool TrySelect(
        OperationDefinition operation,
        bool mutateScenarioState,
        out StubDefaultResponseSelection selection)
    {
        selection = null!;

        var matchedResponse = operation.Responses
            .FirstOrDefault(entry => IsEligibleDefaultResponse(entry.Key, entry.Value));

        if (string.IsNullOrEmpty(matchedResponse.Key) || !int.TryParse(matchedResponse.Key, out var statusCode))
        {
            return false;
        }

        if (!responseBuilder.TryBuild(statusCode, matchedResponse.Value, out var response))
        {
            return false;
        }

        if (mutateScenarioState)
        {
            scenarioService.Advance(matchedResponse.Value.Scenario);
        }

        selection = new StubDefaultResponseSelection(
            matchedResponse.Key,
            statusCode,
            response);
        return true;
    }

    private bool IsEligibleDefaultResponse(string statusCode, ResponseDefinition responseDefinition)
    {
        return int.TryParse(statusCode, out _) &&
               scenarioService.IsMatch(responseDefinition.Scenario) &&
               (responseDefinition.Content.Count > 0 || !string.IsNullOrEmpty(responseDefinition.ResponseFile));
    }
}

internal sealed record StubDefaultResponseSelection(
    string ResponseId,
    int StatusCode,
    StubResponse Response);
