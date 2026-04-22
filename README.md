# SemanticStub

Semantic-aware API mock server.

日本語版: [README.ja.md](README.ja.md)

## Overview

SemanticStub is a semantic-aware API mock server for local development, testing, and AI-assisted workflows.

It combines deterministic OpenAPI-based routing with optional semantic matching, so you can define precise mock behavior while also supporting natural-language fallback scenarios.

### Key features

- OpenAPI 3.1-based YAML stub definitions with `x-*` extensions for SemanticStub-specific behavior.
- Conditional request matching for query strings, headers, JSON bodies, and form-urlencoded bodies.
- Optional semantic matching powered by a Text Embeddings Inference (TEI) endpoint.
- Scenario-based response flows with in-memory state transitions.
- Runtime inspection endpoints for routes, scenarios, metrics, recent requests, and match explanations.
- File-backed responses, response delays, and Docker-based local development support.

## ❤️ Sponsors

If SemanticStub helps your workflow, consider sponsoring 🙌

Your support helps:
- Maintain and improve SemanticStub
- Build related developer tools
- Continue AI-assisted development research

## YAML Extensions

SemanticStub uses OpenAPI 3.1 for the base document structure and `x-*`
fields for custom behavior. Keep standard OpenAPI fields such as `paths`,
operations, `requestBody`, and `responses` unchanged, and place
SemanticStub-specific behavior in extensions only.

### Supported response extensions

These extensions are used on response objects:

| Extension | Location | Purpose |
| --- | --- | --- |
| `x-delay` | `responses.<status>` or `x-match[].response` | Delays the response by the specified milliseconds. |
| `x-response-file` | `responses.<status>` or `x-match[].response` | Loads the response body from a file relative to the YAML file. |
| `x-scenario` | `responses.<status>` or `x-match[].response` | Restricts a response to a named scenario state and can advance that state with `next`. |

Example:

```yaml
openapi: 3.1.0
info:
  title: Response Extensions Example
  version: 1.0.0

paths:
  /users:
    get:
      responses:
        '200':
          description: User list
          x-delay: 100
          x-response-file: users.json
          content:
            application/json:
              schema:
                type: object
```

Notes:

- `x-delay` must be a non-negative integer.
- A response must define `content` or `x-response-file`.
- `x-response-file` keeps the media type declaration in `content`, while the
  payload comes from the referenced file.
- `x-response-file` paths are resolved relative to the YAML file that declares
  them.
- File-backed responses preserve the declared media type, including binary
  types such as `application/octet-stream`.
- When multiple media types are declared, SemanticStub prefers a JSON media
  type for deterministic response selection and otherwise uses the first
  declared type.
- `x-scenario.name` and `x-scenario.state` are required. `x-scenario.next` is
  optional and advances the in-memory scenario state after that response is
  selected.

Scenario example:

```yaml
paths:
  /checkout:
    post:
      responses:
        '409':
          description: pending
          x-scenario:
            name: checkout-flow
            state: initial
            next: confirmed
          content:
            application/json:
              example:
                result: pending
        '200':
          description: complete
          x-scenario:
            name: checkout-flow
            state: confirmed
          content:
            application/json:
              example:
                result: complete
```

Scenario notes:

- Scenario state is stored in memory and shared by scenario name.
- A response without `x-scenario` is always eligible.
- When a response defines `next`, later requests use the advanced state until
  another transition, an automatic stub-definition reload, or an application
  restart resets it.
- `x-scenario` can be used on normal `responses` entries and on
  `x-match[].response` entries.
- Scenario evaluation and state transitions are serialized per process so a
  single scenario flow advances deterministically across concurrent requests.

### Supported operation extensions

`x-match` can be used on an operation to define conditional matches before the
default OpenAPI response entries are used.

```yaml
paths:
  /users:
    get:
      x-match:
        - query:
            role: admin
            region:
              regex: ^ap-.*
          headers:
            X-Env:
              equals: staging
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  users:
                    - id: 1
                      name: Alice
                      role: admin
      responses:
        '200':
          description: Default user list
          content:
            application/json:
              example:
                users: []
  /oauth/token:
    post:
      x-match:
        - body:
            form:
              grant_type:
                equals: authorization_code
              code:
                regex: "^[A-Za-z0-9_-]+$"
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  access_token: token-123
      responses:
        '400':
          description: Invalid token request
```

Each `x-match` entry may contain:

- `query`: query-string matches using scalar `equals` shorthand or explicit
  `equals` / `regex` operators.
- `headers`: header matches using scalar `equals` shorthand or explicit
  `equals` / `regex` operators.
- `body`: JSON body match data or `body.form` form-urlencoded match data.
- `x-semantic-match`: natural-language description used for semantic fallback matching.
- `response`: the response returned when the match succeeds.

Notes:

- `response.statusCode` is required and must be an HTTP status code between 100 and 599.
- `x-match` responses support the same `content`, `headers`, `x-delay`, and
  `x-response-file` fields as normal responses.
- Query, header, and body conditions are combined with AND semantics inside a
  single `x-match` entry.
- Query and header keys used by `x-match` must reference parameters declared in
  OpenAPI when those parameters are present on the path or operation.
- Scalar `query` and `headers` values are shorthand for `equals`.
- `query.equals` supports exact single-value matches, ordered repeated values,
  and typed comparison for declared OpenAPI query parameter types such as
  `integer`, `number`, and `boolean`.
- `query.regex` and `headers.regex` perform regex matching. Use regex patterns
  such as `.*value.*`, `^value`, or `value$` for contains, starts-with, or
  ends-with matches.
- JSON `body` matching is partial for objects, so a request may contain
  additional properties and still match.
- Use `body.form` for `application/x-www-form-urlencoded` request bodies. Scalar
  form values are shorthand for `equals`, and form fields may also use explicit
  `equals` / `regex` operators. Configured form keys must exist, and additional
  request form keys are allowed.
- `body.form` cannot be combined with `body.json` or `body.text` in the same
  match entry.
- Invalid JSON request bodies do not satisfy `body` match conditions.
- `x-semantic-match` entries are evaluated only after all deterministic
  conditions fail. They require semantic matching to be enabled in application
  configuration. An entry with `x-semantic-match` must not be combined with
  `query`, `headers`, or `body`.
- When no `x-match` entry succeeds, SemanticStub falls back to the standard
  `responses` section.
- When multiple `x-match` entries succeed, SemanticStub chooses the most
  specific candidate so narrower conditions win over broader ones.

### Semantic matching

When all deterministic `x-match` candidates fail, SemanticStub can fall back to
semantic matching. Each `x-match` entry that contains only `x-semantic-match` is
scored against the incoming request using vector embeddings from a
[Text Embeddings Inference](https://huggingface.co/docs/text-embeddings-inference/en/index)
endpoint. The candidate with the highest cosine similarity above the configured
threshold is selected.

Example:

```yaml
paths:
  /search:
    post:
      x-match:
        - x-semantic-match: find administrator user accounts in the identity directory by email address
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  result: admin-user
        - x-semantic-match: show unpaid billing invoices due this month
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  result: due-invoices
      responses:
        "404":
          description: No match found
          content:
            application/json:
              example:
                message: no match
```

Configure semantic matching in `appsettings.json`:

```json
"StubSettings": {
  "SemanticMatching": {
    "Enabled": true,
    "Endpoint": "http://localhost:8081",
    "Threshold": 0.85,
    "TopScoreMargin": 0,
    "TimeoutSeconds": 30
  }
}
```

| Setting | Description | Default |
| --- | --- | --- |
| `Enabled` | Enables semantic matching fallback. | `false` |
| `Endpoint` | Base URL of the TEI endpoint. | `""` |
| `Threshold` | Minimum cosine similarity to accept a match (-1.0–1.0). | `0.85` |
| `TopScoreMargin` | Minimum score gap between the top two candidates; `0` disables the ambiguity check. | `0` |
| `TimeoutSeconds` | HTTP request timeout for the embedding endpoint. | `30` |

Semantic matching notes:

- The full incoming request (method, path, query parameters, headers, and body)
  is used as the query text for embedding.
- When the embedding service is unavailable or times out, semantic matching is
  skipped and the request falls through to the standard `responses` section.

### Matching precedence

Request handling follows these precedence rules:

1. Exact path match over template path match.
2. Matching HTTP method on the selected path.
3. Matching `x-match` candidate on the operation, preferring more specific
   exact query conditions before broader candidates.
4. Semantic matching fallback when no deterministic `x-match` candidate
   succeeds and semantic matching is enabled.
5. Fallback to the standard OpenAPI `responses` section.

This keeps routing deterministic for the current feature set.

### Current limitations

- `body` matching is intended for structured JSON request payloads rather than
  arbitrary binary request bodies.

## Runtime inspection

SemanticStub exposes runtime inspection endpoints under the reserved prefix
`/_semanticstub/runtime`.

- `GET /_semanticstub/runtime/config` returns metadata for the active effective configuration snapshot.
- `GET /_semanticstub/runtime/routes` returns the active normalized route list.
- `GET /_semanticstub/runtime/routes/{routeId}` returns the effective runtime details for one active route.
- `GET /_semanticstub/runtime/scenarios` returns the current scenario state snapshot.
- `GET /_semanticstub/runtime/metrics` returns aggregate runtime metrics for real requests handled by the current process.
- `POST /_semanticstub/runtime/metrics/resets` resets aggregate runtime metrics and recent request history for the current process. `POST /_semanticstub/runtime/metrics/reset` remains supported for compatibility.
- `GET /_semanticstub/runtime/requests?limit=20` returns a bounded recent request history for real requests handled by the current process.
- `POST /_semanticstub/runtime/test-match` evaluates a virtual request without executing a real response or mutating scenario state.
- `POST /_semanticstub/runtime/explain` returns structured match details for a virtual request, including deterministic and semantic evaluation when applicable.
- `GET /_semanticstub/runtime/explain/last` returns the latest explanation captured from a real matched request in the current process.
- `POST /_semanticstub/runtime/scenarios/resets` resets all configured scenarios back to their initial state. `POST /_semanticstub/runtime/scenarios/reset` remains supported for compatibility.
- `POST /_semanticstub/runtime/scenarios/{name}/resets` resets one configured scenario back to its initial state. `POST /_semanticstub/runtime/scenarios/{name}/reset` remains supported for compatibility.
- YAML stub definitions under `/_semanticstub/runtime/*` are reserved for these inspection endpoints and are not reachable as normal stub routes.

Inspection notes:

- `/_semanticstub/runtime/config` is a summary view. It currently returns snapshot metadata such as timestamp, configuration hash, definitions directory, route count, and whether semantic matching is enabled.
- `/_semanticstub/runtime/routes` returns one item per active path and HTTP method with stable external fields such as route id, normalized path pattern, semantic matching usage, scenario usage, and response count.
- `/_semanticstub/runtime/routes/{routeId}` expands a single route into a stable detail view with top-level responses and normalized conditional match metadata.
- `/_semanticstub/runtime/scenarios` returns one item per known scenario with its current state and whether it is active.
- `/_semanticstub/runtime/metrics` is process-local and currently returns total request count, matched and unmatched counts, fallback and semantic counts, average latency, status-code summaries, and top routes.
- `/_semanticstub/runtime/metrics/resets` and `/_semanticstub/runtime/metrics/reset` clear process-local aggregate metrics and recent request history without reloading configuration, changing scenario state, or clearing `/_semanticstub/runtime/explain/last`.
- `/_semanticstub/runtime/requests` is process-local and currently returns up to 100 recent real requests in newest-first order. Each item includes timestamp, method, path, resolved route id when available, status code, elapsed time, match mode, and failure reason for unmatched requests. The `limit` query parameter defaults to `20`.
- `/_semanticstub/runtime/test-match` and `/_semanticstub/runtime/explain` accept a virtual request payload with method, path, optional query/header/body values, and optional candidate-detail flags.
- `/_semanticstub/runtime/explain/last` is process-local and only updates after a real request produces a matched stub response.
- `/_semanticstub/runtime/scenarios/resets`, `/_semanticstub/runtime/scenarios/reset`, `/_semanticstub/runtime/scenarios/{name}/resets`, and `/_semanticstub/runtime/scenarios/{name}/reset` mutate only in-memory scenario state for the current process.
- These endpoints do not expose raw YAML, internal domain objects, or full response payload bodies.

Example request body for `POST /_semanticstub/runtime/test-match` and
`POST /_semanticstub/runtime/explain`:

```json
{
  "method": "GET",
  "path": "/users",
  "query": {
    "role": ["admin"]
  },
  "includeCandidates": true
}
```

Excerpt from the response body for `GET /_semanticstub/runtime/routes/listUsers`:

```json
{
  "routeId": "listUsers",
  "method": "GET",
  "pathPattern": "/users",
  "usesSemanticMatching": false,
  "usesScenario": false,
  "responseCount": 1,
  "hasConditionalMatches": true,
  "responses": [
    {
      "responseId": "200",
      "delayMilliseconds": 100,
      "usesScenario": false,
      "scenario": null
    }
  ],
  "conditionalMatches": [
    {
      "candidateIndex": 0,
      "hasExactQuery": true,
      "exactQueryKeys": ["role"],
      "hasPartialQuery": false,
      "partialQueryKeys": [],
      "hasRegexQuery": false,
      "regexQueryKeys": [],
      "headerKeys": [],
      "hasBody": false,
      "usesSemanticMatching": false,
      "responseStatusCode": 200,
      "delayMilliseconds": null,
      "usesScenario": false,
      "scenario": null
    }
  ]
}
```

Example response body for `GET /_semanticstub/runtime/requests?limit=1`:

```json
[
  {
    "timestamp": "2026-04-08T00:00:00Z",
    "method": "GET",
    "path": "/users",
    "routeId": "listUsers",
    "statusCode": 200,
    "elapsedMilliseconds": 12.3,
    "matchMode": "exact",
    "failureReason": null
  }
]
```

## Development
- Source: `src/`
- Tests: `tests/`
- Samples: `samples/`

Configuration notes:

- `appsettings.json` provides the default runtime settings.
- `appsettings.Development.json` is applied only when the app runs with the `Development` environment, for example to enable local semantic matching settings or more verbose logging.
- The sample requests in `SemanticStub.http` assume the app is running locally and may be easier to verify with the `Development` environment enabled.

Sample files:

- `samples/basic-routing.yaml` demonstrates the core routing, matching, scenario, and file-response features.
- `samples/semantic-search.stub.yaml` demonstrates semantic matching routes and is useful when testing the `SemanticMatching` settings locally.

## Run

Default:

```sh
dotnet run --project src/SemanticStub.Api
```

Run with the Development environment so `appsettings.Development.json` is applied:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SemanticStub.Api
```

## Docker

Build the Docker-based setup:

```sh
docker compose build
```

Run SemanticStub and the embedding service in the background:

```sh
docker compose up -d tei semantic-stub
```

This setup exposes SemanticStub on `http://localhost:8080`. The `samples/`
directory is mounted into the container, so editing stub YAML files does not
require rebuilding the image. The TEI service stays on the internal Docker
network only.

Add the MCP server to Claude Desktop:

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

## Agent Skill

A skill is available for efficient use of the MCP tools. It supports any agent compatible with the [Agent Skills](https://agentskills.io) specification, including Claude Code, GitHub Copilot, Cursor, and others.

### Using with gh skill

```sh
gh skill install winuim/SemanticStub semantic-stub
```

### Manual install for Claude Desktop

1. Download `skills/semantic-stub.skill`
2. Open Claude Desktop → Customize → Skills
3. Click the `+` button → `+ Create skill`
4. Select `Upload a skill`
5. Upload the `.skill` file

## Test

```sh
dotnet test
```

Collect coverage in Cobertura format:

```sh
dotnet test --collect:"XPlat Code Coverage"
```

Write coverage results to a fixed directory:

```sh
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Optionally generate an HTML report from the Cobertura output with ReportGenerator:

```sh
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```

## Notes
See `AGENTS.md` for repository guidance.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
