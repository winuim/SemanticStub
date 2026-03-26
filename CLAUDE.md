# CLAUDE.md

## Project

**SemanticStub** — A semantic-aware API mock server.
Supports YAML-based stub definitions with optional semantic matching using embeddings.

---

## MUST FOLLOW

- YAML must follow OpenAPI 3.1 structure
- Use `x-*` for all custom extensions
- Do NOT break existing YAML compatibility
- Prefer minimal, safe changes
- Do NOT refactor unrelated code
- Do NOT change working behavior unless explicitly required
- Prefer compatibility over redesign
- When unsure, choose the safest implementation and explain assumptions

---

## Workflow

Before coding, always:
1. Create a feature branch (see Git / Commit Rules)
2. Read relevant code
3. Identify affected files, risks, and edge cases
4. Clarify assumptions — do not start coding blindly
5. Create a plan
6. Implement minimal change
7. Run validation (see below)
8. Self-review the diff against code_review.md
9. Summarize results using the Output Format below

---

## Validation Commands

```bash
dotnet restore
dotnet build
dotnet test
```

---

## Solution Structure

- Solution file at repository root: `SemanticStub.sln`
- All projects under `src/` and `tests/`

---

## Architecture Overview

### Core Concepts

| Component | Role |
|---|---|
| StubDefinition | YAML-defined API behavior |
| RouteTable | Compiled routing structure |
| Matcher | Evaluates requests |
| ResponseBuilder | Builds responses |
| Scenario | Optional state machine |

### Layering

- **Controllers**: catch-all HTTP entry
- **Services**: StubService / MatcherService / ScenarioService
- **Infrastructure**: YAML loader / File watcher / Route builder

---

## OpenAPI / YAML Rules

- Use OpenAPI 3.1.x
- Standard OpenAPI fields only: `paths`, `methods`, `parameters`, `requestBody`, `responses`, `headers`, `content`, `schema`
- Custom behavior via `x-*` extensions only:
  - `x-delay`
  - `x-scenario`
  - `x-response-file`
  - `x-semantic-match`
- Do NOT rename OpenAPI fields, redefine existing concepts, or introduce alternative structures
- YAML is the source of truth — keep definitions predictable and human-readable
- Do not duplicate meaning across fields
- Prefer readability and consistency over compactness

---

## Routing Rules

- Use catch-all route: `/{*path}`
- Matching priority:
  1. Exact match
  2. Pattern match
  3. Semantic match (optional)
- Routing must be deterministic unless semantic mode is enabled

---

## Matching Rules

- HTTP method must match exactly
- Headers are optional match conditions
- Query parameters may support exact or regex matching
- Body may support JSON / string / binary
- Avoid complex matching unless necessary

---

## Response Rules

- Support: status code, headers, body, file-based response, delay
- Do NOT convert binary to string
- Prefer streaming for large payloads

---

## Scenario Rules

- Scenario is optional
- State transitions must be explicit
- Ensure thread safety
- Avoid dynamic typing

---

## ASP.NET Core Guidance

- Follow existing Controller / Service structure
- Use DI via constructor injection
- Prefer `async`/`await` for I/O
- Do not change middleware order without reason
- Preserve HTTP behavior compatibility

---

## Code Style

- Add comments where intent is not obvious — explain **why**, not what
- Avoid redundant comments
- Document complex logic and design decisions
- Prefer clear and readable code over clever code
- Prefer explicit behavior over implicit behavior

---

## Logging

- Use `ILogger<T>`
- Log: matched route, match decisions, scenario transitions, semantic score (if used)
- Avoid excessive logging in normal flow

---

## Performance

- Precompile routing where possible
- Avoid heavy allocations per request
- Cache where safe
- Keep semantic matching optional

---

## Container / Deployment

- Must run in container
- Must support read-only filesystem
- Avoid OS-specific assumptions
- Use configurable file paths

---

## Dependency Management

- Prefer latest compatible versions
- Avoid upgrading major versions unless explicitly requested
- Use `Directory.Packages.props` for centrally managed explicit versions
- Ensure build and tests pass after updates

---

## Git / Commit Rules

Use conventional commits format:

```
<type>: <summary>
```

Allowed types: `feat` / `fix` / `refactor` / `test` / `chore` / `docs`

Examples:
```
feat: add query parameter matching
fix: handle missing route correctly
test: add controller tests
chore: update dependencies
```

- Keep commits small and logical
- Group related changes into logical commits
- Create checkpoints before risky changes
- Review diffs before completion
- Always work on a feature branch — never commit directly to `develop`
- Branch naming: `feature/<issue-or-description>` (e.g. `feature/issue-23-non-json-content-types`)
- Open a PR from the feature branch into `develop`

---

## Pull Request Rules

- PR title must follow conventional commits format
- PR description must summarize: purpose, changes, validation, impact
- Target branch is always `develop`
- Address review feedback with new commits on the same branch — do not amend

---

## Testing Rules

- Add tests for behavior changes
- Prefer unit tests
- Add regression tests for bug fixes
- Do not remove tests without reason

---

## Change Boundaries

Unless explicitly requested, do NOT:
- Rename or move files
- Change YAML schema
- Modify unrelated code
- Update dependencies
- Introduce new dependencies

---

## Semantic Matching

- Must be optional and must not break deterministic mode
- Threshold must be configurable
- Log similarity score

---

## Approval Required

Always ask before:
- Breaking YAML compatibility
- Changing routing behavior
- Adding new abstractions
- Introducing external services
- Changing API contracts
- Changing infrastructure

---

## Safety

- Highlight ambiguity before large changes
- Call out breaking changes clearly
- Prefer safe implementation choices

---

## Output Format

### Summary
What changed and why.

### Files Changed
List of modified files.

### Validation
Commands run and results.

### Notes
- Risks
- Assumptions
- Follow-ups
- Compatibility impact
- YAML changes
- Performance impact

---

## Task-Specific Guidelines

### Fixing Bugs
1. Identify root cause first
2. Apply minimal fix
3. Add regression test

### Adding Features
1. Fit into existing architecture
2. Avoid over-engineering
3. Use `x-*` for YAML extensions
