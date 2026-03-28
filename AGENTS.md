# AGENTS.md

## MUST FOLLOW

- YAML must follow OpenAPI 3.1 structure
- Use `x-*` for all custom extensions
- Do NOT break existing YAML compatibility
- Prefer minimal, safe changes
- Do NOT refactor unrelated code
- Do NOT change working behavior unless explicitly required
- Prefer compatibility over redesign
- When unsure, choose the safest implementation and explain assumptions
- Use conventional commits format for commit messages and PR titles
- Group related changes into logical commits
- Do not create overly large commits
- Prefer small, reviewable commits
- Prefer reading interfaces, tests, and docs before reading concrete implementations
- Do not open implementation files unless interface, tests, or docs are insufficient
- Keep context usage small by limiting investigation to the minimum affected area
- When starting work, identify the smallest set of files needed and avoid broad repository scans

---

## Project
SemanticStub

A semantic-aware API mock server.
Supports YAML-based stub definitions with optional semantic matching using embeddings.

---

## OpenAPI / YAML Rules

- Use OpenAPI 3.1.x
- Base structure must follow OpenAPI specification
- Use standard OpenAPI fields for:
  - paths
  - methods (get, post, etc)
  - parameters
  - requestBody
  - responses
  - headers
  - content
  - schema

- Custom behavior must be defined using `x-*` extensions only
  - Examples:
    - x-delay
    - x-scenario
    - x-response-file
    - x-semantic-match

- Do NOT:
  - rename OpenAPI fields
  - redefine existing concepts
  - introduce alternative structures

- Prefer readability and consistency over compactness

---

## Solution Structure

- Place the solution file at the repository root
- Solution name must be `SemanticStub.sln`
- Include all projects under `src/` and `tests/`

---

## Architecture Overview

### Core Concepts
- StubDefinition: YAML-defined API behavior
- RouteTable: compiled routing structure
- Matcher: evaluates requests
- ResponseBuilder: builds responses
- Scenario: optional state machine

### Layering
- Controllers: catch-all HTTP entry
- Services:
  - StubService
  - MatcherService
  - ScenarioService
- Infrastructure:
  - YAML loader
  - File watcher
  - Route builder

---

## General Rules

- Follow existing project structure and naming
- Do not introduce new dependencies unless necessary
- Avoid large refactors unless explicitly requested
- Keep code simple and reviewable
- Prefer explicit behavior over implicit behavior

---

## Code Style

### General
- Prefer clear and readable code over clever code
- Keep code simple and maintainable

### Documentation
- Add comments where intent is not obvious
- Explain "why", not "what"
- Avoid redundant comments
- Document complex logic and design decisions

### XML Documentation
- Add XML documentation comments for:
  - All public classes
  - All public methods

- Focus on:
  - Purpose and responsibility
  - Important behavior and constraints
  - Side effects (e.g. external API calls, state changes)

- Do NOT add comments for trivial code or DTOs

### Parameters and Returns
- Only document parameters when:
  - Format, unit, or constraints are important
  - Behavior is not obvious from the method signature

- Only document return values when:
  - Nullability or special cases exist
  - Behavior is not obvious

---

## YAML Design Rules

- YAML is the source of truth
- Prefer OpenAPI-compatible structure
- Use `x-*` only for extensions
- Do not duplicate meaning across fields
- Keep definitions predictable and human-readable

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

- Support:
  - status code
  - headers
  - body
  - file-based response
  - delay

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
- Prefer async/await for I/O
- Do not change middleware order without reason
- Preserve HTTP behavior compatibility

---

## Logging

- Use `ILogger<T>`
- Log:
  - matched route
  - match decisions
  - scenario transitions
  - semantic score (if used)

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
- If using Directory.Packages.props:
  - prefer explicit versions
  - keep versions centrally managed in Directory.Packages.props
- Ensure build and tests pass after updates

---

## Workflow

1. Identify the smallest relevant set of files
2. Read interfaces, tests, and docs first
3. Read concrete implementations only when necessary
4. Create a plan
5. Implement minimal change
6. Run validation
7. Summarize results

---

## Planning Expectations

- Identify the smallest affected file set first
- Prefer interface, test, and documentation review before implementation review
- Identify risks and edge cases
- Clarify assumptions
- Avoid broad repository exploration unless clearly required
- Do not start coding blindly

---

## Testing Rules

- Add tests for behavior changes
- Prefer unit tests
- Add regression tests for bug fixes
- Do not remove tests without reason

---

## Validation Commands

- dotnet restore
- dotnet build
- dotnet test

---

## Change Boundaries

Unless explicitly requested:
- Do not rename files
- Do not move files
- Do not change YAML schema
- Do not modify unrelated code
- Do not update dependencies

---

## Git Expectations

- Create checkpoints before risky changes
- Keep commits small and logical
- Review diffs before completion

---

## Commit Rules

- Use conventional commits format for all commit messages
- Format: <type>: <summary>
- Allowed commit types:
  - feat
  - fix
  - refactor
  - test
  - chore
  - docs

Examples:
- feat: add query parameter matching
- fix: handle missing route correctly
- test: add controller tests
- chore: update dependencies

- Keep summary short and descriptive
- Do not include unnecessary details in the title

---

## Pull Request Rules

- PR title must follow conventional commits format
- PR description should summarize:
  - purpose
  - changes
  - validation
  - impact
- When addressing review feedback:
  - Reply in the relevant review thread with a brief summary of the fix
  - Include the validation result when it helps the reviewer confirm the change
  - Prefer resolving the review thread after the fix is pushed and verified
  - Do not rely only on a top-level PR comment when a thread-specific reply is more appropriate

---

## Output Format

### Summary
- What changed

### Files Changed
- List of files

### Validation
- Commands run
- Results

### Notes
- Risks
- Assumptions
- Follow-ups
- Compatibility impact
- YAML changes
- Performance impact

---

## When Fixing Bugs

- Identify root cause first
- Prefer minimal fix
- Add regression test

---

## When Adding Features

- Fit into existing architecture
- Avoid over-engineering
- Use `x-*` for extensions

---

## Semantic Matching

- Must be optional
- Must not break deterministic mode
- Threshold must be configurable
- Log similarity score

---

## Approval Required

Ask before:
- breaking YAML compatibility
- changing routing behavior
- adding new abstractions
- introducing an interface only for style or speculative future use
- introducing external services
- changing API contracts
- changing infrastructure

---

## Safety

- Highlight ambiguity before large changes
- Call out breaking changes clearly
- Prefer safe implementation choices

## Coding Guidelines for AI Agent (.NET / C#)

### Context Efficiency
- Prefer the smallest possible reading scope for each task
- Read public contracts before concrete implementations
- Treat interfaces, XML documentation, tests, and architecture notes as primary sources of behavior
- Only inspect implementation details when behavior cannot be determined from the contract or tests
- Avoid repository-wide searches when the affected area is already known

### General Principles
- Write clean, readable, and maintainable code
- Prefer simplicity over cleverness
- Follow existing project structure and conventions
- Avoid unnecessary abstractions
- Prefer explicit contracts over implicit behavior
- Introduce interfaces when they reduce coupling, improve testability, or clarify boundaries
- Do not introduce interfaces that only mirror a single implementation without a clear architectural or testing benefit

---

## Documentation Rules

### XML Comments
- Add XML documentation comments for:
  - All public classes
  - All public methods
  - Public interface members when behavior or constraints are important to consumers

### What to Write
- Focus on WHY, not just WHAT
- Describe:
  - Purpose and responsibility of the class
  - Important behavior or business logic
  - Constraints or assumptions
  - Side effects (e.g. external API calls, state changes)
  - Nullability and special return behavior when not obvious
  - Whether callers can rely on the method contract without reading the implementation

### Parameters and Return Comments
- Do NOT add comments for obvious parameters or return values
- Only document when:
  - Format or unit is important (e.g. yyyyMMdd, seconds)
  - Constraints exist (e.g. must be authenticated)
  - Nullability or special return behavior exists
  - Behavior is not clear from method signature

### Avoid
- Redundant comments that restate the code
- Commenting trivial DTOs or simple CRUD methods

---

## Code Style

### Naming
- Use clear and descriptive names
- Avoid abbreviations unless commonly understood

### Methods
- Keep methods small and focused
- Prefer single responsibility

### Classes
- Each class should have a clear responsibility
- Avoid god classes

---

## Dependency Injection
- Use constructor injection
- Avoid service locator pattern
- Do not resolve dependencies manually unless explicitly required

### Interface and Contract Design
- Prefer interface-first design at service boundaries that are reused, tested in isolation, or likely to vary by infrastructure
- Keep interfaces small and focused on a single responsibility
- Do not create fat interfaces with unrelated operations
- Public service contracts should make important behavior clear through naming, XML documentation, and tests
- Document notable constraints, side effects, nullability, and exceptional behavior when they are not obvious from the signature
- If a concrete class is sufficient and no boundary benefit exists, prefer the simpler concrete dependency

---

## Error Handling
- Use exceptions for exceptional cases
- Do not swallow exceptions silently
- Add meaningful context to exceptions

---

## Logging
- Log important operations and failures
- Do not log sensitive information
- Avoid excessive logging

---

## HTTP / External Calls
- Use IHttpClientFactory
- Do not create HttpClient manually
- Handle timeouts and retries where appropriate

---

## Testing
- Write unit tests for business logic
- Mock external dependencies
- Do not test framework behavior

---

## When Generating Code
- Do not modify unrelated files
- Only change what is necessary
- If multiple approaches exist, choose the simplest one

---

## Code Review Expectations
- Prioritize correctness, readability, and maintainability
- Flag unclear logic or missing documentation
- Suggest improvements when confidence is high

---

## Code Review Policy

You MUST always post a review comment.

- If issues are found, describe them clearly.
- If no issues are found, you MUST respond with:
  "No issues found"