# SemanticStub

Semantic-aware API mock server.

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
```

Each `x-match` entry may contain:

- `query`: exact query-string matches.
- `headers`: exact header matches.
- `body`: exact body match data.
- `response`: the response returned when the match succeeds.

Notes:

- `response.statusCode` is required and must be a positive integer.
- `x-match` responses support the same `content`, `headers`, `x-delay`, and
  `x-response-file` fields as normal responses.
- When no `x-match` entry succeeds, SemanticStub falls back to the standard
  `responses` section.

### Sample-only extensions

The repository also includes examples that use the following extension names:

| Extension | Sample | Intent |
| --- | --- | --- |
| `x-scenario` | `samples/scenario-basic.yaml` | Describes scenario/state-based response behavior. |
| `x-semantic-match` | `samples/semantic-search.yaml` | Describes semantic matching behavior for request content. |

These samples document the intended OpenAPI-compatible extension style for
higher-level features. See the sample files for the exact YAML shape used in
this repository.

## Development
- Source: `src/`
- Tests: `tests/`
- Samples: `samples/`

## Run
dotnet run --project src/SemanticStub.Api

## Test
dotnet test

## Notes
See `AGENTS.md` for repository guidance.
