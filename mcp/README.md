# semantic-stub-mcp

A TypeScript MCP server for SemanticStub's Runtime Inspection API.
It exposes runtime inspection, match simulation, and scenario reset as tools so it works well with tool-centric clients such as Claude Desktop.

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
| `get_config` | `GET /runtime/config` | Configuration snapshot metadata |
| `list_routes` | `GET /runtime/routes` | Active route list |
| `get_route` | `GET /runtime/routes/{id}` | Detailed route information |
| `get_scenarios` | `GET /runtime/scenarios` | Current scenario state |
| `get_metrics` | `GET /runtime/metrics` | Runtime metrics |
| `reset_metrics` | `POST /runtime/metrics/reset` | Reset runtime metrics and recent request history |
| `get_requests` | `GET /runtime/requests?limit=` | Recent request history with limit |
| `test_match` | `POST /runtime/test-match` | Match simulation without side effects |
| `explain_match` | `POST /runtime/explain` | Detailed match explanation |
| `get_last_explain` | `GET /runtime/explain/last` | Latest real-request explanation |
| `reset_scenario_state` | `POST /runtime/scenarios/reset` / `POST /runtime/scenarios/{name}/reset` | Reset scenario state |

## Input Notes

- The `body` field for `test_match` and `explain_match` must be a raw string, not a JSON object.
- If you want to send JSON content, stringify it first, for example `"{\"message\":\"hello\"}"`.
- Set `includeSemanticCandidates` to include semantic candidate scores when semantic matching is attempted.

## Constraints

- `metrics`, `requests`, `get_last_explain`, and `scenarios` return process-local runtime state.
- `reset_metrics` clears aggregate metrics and recent request history only. It does not reset scenario state, reload active stub definitions, or clear the latest real-request explanation.
- This MCP server is a thin bridge to the SemanticStub HTTP API and does not change YAML or core behavior.

## Development

```bash
# Run without building
npm run dev
```

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SEMANTIC_STUB_URL` | `http://localhost:8080` | Base URL of the SemanticStub API |
