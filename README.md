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
- `x-query-partial`: partial query-string matches.
- `headers`: exact header matches.
- `body`: exact body match data.
- `response`: the response returned when the match succeeds.

Notes:

- `response.statusCode` is required and must be a positive integer.
- `x-match` responses support the same `content`, `headers`, `x-delay`, and
  `x-response-file` fields as normal responses.
- Query, header, and body conditions are combined with AND semantics inside a
  single `x-match` entry.
- Query and header keys used by `x-match` must reference parameters declared in
  OpenAPI when those parameters are present on the path or operation.
- `query` supports exact single-value matches, ordered repeated values, and
  typed comparison for declared OpenAPI query parameter types such as
  `integer`, `number`, and `boolean`.
- `x-query-partial` performs substring matching. Exact `query` matches are
  preferred over partial matches when multiple candidates succeed.
- `body` matching currently applies to JSON request bodies. Body matching is
  partial for objects, so a request may contain additional properties and still
  match.
- Invalid JSON request bodies do not satisfy `body` match conditions.
- When no `x-match` entry succeeds, SemanticStub falls back to the standard
  `responses` section.
- When multiple `x-match` entries succeed, SemanticStub chooses the most
  specific candidate so narrower conditions win over broader ones.

### Matching precedence

Request handling follows these precedence rules:

1. Exact path match over template path match.
2. Matching HTTP method on the selected path.
3. Matching `x-match` candidate on the operation, preferring more specific
   exact query conditions before broader candidates.
4. Fallback to the standard OpenAPI `responses` section when no `x-match`
   candidate succeeds.

This keeps routing deterministic for the current feature set.

### Current limitations

- Regex query matching is not supported yet.
- Semantic matching appears only in sample files today; it is not part of the
  current runtime behavior.
- `body` matching is intended for structured JSON request payloads rather than
  arbitrary binary request bodies.

### Sample-only extensions

The repository also includes examples that use the following extension names:

| Extension | Sample | Intent |
| --- | --- | --- |
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
