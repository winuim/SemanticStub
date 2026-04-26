# semantic-stub-mcp

A TypeScript MCP server for SemanticStub's Runtime Inspection API.
It exposes runtime inspection, match simulation, stub generation, match improvement suggestions, and scenario reset as tools so it works well with tool-centric clients such as Claude Desktop.

Japanese documentation is available in [README.ja.md](./README.ja.md).

For the full setup including SemanticStub and TEI, see the repository root
[README.md](../README.md). This file describes how to run the MCP server on its own.

## Requirements

- Node.js 18+
- A running SemanticStub API instance

## Setup

```bash
cd mcp
npm install
npm run build
```

## Claude Desktop Configuration

Add this server to `claude_desktop_config.json`.

**macOS**
```
~/Library/Application Support/Claude/claude_desktop_config.json
```

```json
{
  "mcpServers": {
    "semantic-stub": {
      "command": "node",
      "args": ["/path/to/SemanticStub/mcp/dist/index.js"],
      "env": {
        "SEMANTIC_STUB_URL": "http://localhost:8080"
      }
    }
  }
}
```

## Available Tools

| Tool | Endpoint | Description |
|---|---|---|
| `get_config` | `GET /_semanticstub/runtime/config` | Configuration snapshot metadata |
| `list_routes` | `GET /_semanticstub/runtime/routes` | Active route list |
| `get_route` | `GET /_semanticstub/runtime/routes/{id}` | Detailed route information |
| `get_scenarios` | `GET /_semanticstub/runtime/scenarios` | Current scenario state |
| `get_metrics` | `GET /_semanticstub/runtime/metrics` | Runtime metrics |
| `reset_metrics` | `POST /_semanticstub/runtime/metrics/resets` | Reset runtime metrics and recent request history |
| `get_requests` | `GET /_semanticstub/runtime/requests?limit=` | Recent request history with limit |
| `test_match` | `POST /_semanticstub/runtime/test-match` | Match simulation without side effects |
| `explain_match` | `POST /_semanticstub/runtime/explain` | Detailed match explanation |
| `get_last_explain` | `GET /_semanticstub/runtime/explain/last` | Latest real-request explanation |
| `reset_scenario_state` | `POST /_semanticstub/runtime/scenarios/resets` / `POST /_semanticstub/runtime/scenarios/{name}/resets` | Reset scenario state |
| `export_stubs_as_yaml` | `GET /_semanticstub/runtime/requests/export/yaml` / `GET /_semanticstub/runtime/requests/{index}/export/yaml` | Export recorded requests as draft YAML stub definitions |
| `suggest_improvements` | `GET /_semanticstub/runtime/requests/{index}/suggest-improvements` / `POST /_semanticstub/runtime/suggest-improvements` | Suggest YAML improvements for ambiguous or low-quality stub matches |

## Input Notes

- The `body` field for `test_match`, `explain_match`, and `suggest_improvements` must be a raw string, not a JSON object.
- If you want to send JSON content, stringify it first, for example `"{\"message\":\"hello\"}"`.
- `test_match` defaults `includeCandidates` to `false`, while `explain_match` defaults it to `true`.
- Set `includeSemanticCandidates` to include semantic candidate scores when semantic matching is attempted.
- The result payload for `test_match` and `explain_match` includes selected response metadata such as response id, status code, source (`responses` or `x-match`), and candidate index when applicable.
- `export_stubs_as_yaml` returns raw YAML text. The output is a reviewable draft — fill in the `TODO` placeholders before activating.
- `suggest_improvements` accepts either an `index` (to analyze a recorded request) or `method` + `path` (to analyze a virtual request). When using `method`/`path`, both are required.

## Constraints

- `metrics`, `requests`, `get_last_explain`, and `scenarios` return process-local runtime state.
- `reset_metrics` clears aggregate metrics and recent request history only. It does not reset scenario state, reload active stub definitions, or clear the latest real-request explanation.
- This MCP server is a thin bridge to the SemanticStub HTTP API and does not change YAML or core behavior.

## Development

```bash
# Run without building
npm run dev
```

The inspection API contract used by this MCP package is regression-tested in
[`tests/SemanticStub.Api.Tests/Integration/StubInspectionEndpointTests.cs`](../tests/SemanticStub.Api.Tests/Integration/StubInspectionEndpointTests.cs).

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SEMANTIC_STUB_URL` | `http://localhost:8080` | Base URL of the SemanticStub API |
