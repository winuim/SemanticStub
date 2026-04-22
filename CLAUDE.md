# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@AGENTS.md

## Commands

```sh
# Run the application
dotnet run --project src/SemanticStub.Api

# Run with Development environment (enables appsettings.Development.json)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SemanticStub.Api

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/SemanticStub.Application.Tests

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Format code
dotnet format

# Verify formatting (CI)
dotnet format --verify-no-changes

# Docker
docker compose build
docker compose up -d tei semantic-stub
```

## Architecture

SemanticStub is an ASP.NET Core API mock server. It loads OpenAPI 3.1 YAML stub definitions and serves responses based on deterministic routing with optional semantic (vector embedding) fallback matching.

### Layers

- **`SemanticStub.Api`** — HTTP entry point: controllers, middleware, request/response shaping. No business logic here.
- **`SemanticStub.Application`** — Business logic and orchestration: matching, scenario state, response selection, route compilation. Depends only on abstractions (interfaces).
- **`SemanticStub.Infrastructure`** — Concrete implementations: YAML parsing, embedding HTTP client (`SemanticEmbeddingClient`), file I/O.

Dependency rules: `Api → Application ← Infrastructure`. Application must not reference Infrastructure types directly.

### Request Handling Flow

`StubController` → `StubService` → route resolution (`StubRouteResolver`) → conditional matching (`StubDispatchSelector`) → semantic fallback → response building (`StubResponseBuilder`). Inspection endpoints are handled separately via `StubInspectionController`.

### Key Subsystems

- **`x-match` matching** (`Application/Services/Matching/`): evaluates query, header, body, and form conditions with AND semantics; selects the most specific match.
- **Scenario state** (`Application/Services/Scenario/`): in-memory state machine per named scenario; transitions are serialized to be deterministic.
- **Semantic matching** (`Infrastructure/Semantic/`): calls a TEI endpoint for cosine similarity; only runs when all deterministic matches fail.
- **Inspection endpoints** (`Api/Inspection/`, `Api/Controllers/StubInspectionController.cs`): `/_semanticstub/runtime/*` routes expose routes, scenarios, metrics, and match explanations.

### Configuration

Settings live in `appsettings.json`; `appsettings.Development.json` applies only in the `Development` environment. Use strongly-typed Options classes (`IOptions<T>`); never inject `IConfiguration` into services. Configuration section is `StubSettings`.

## Testing

- **Unit tests** (`Application.Tests`): cover matchers, route parsing, scenario transitions, response selection via public APIs. Use xUnit + Moq + Shouldly.
- **Integration tests** (`Api.Tests`): use `WebApplicationFactory`; test HTTP pipeline, routing, and DI wiring. Replace only truly external dependencies.
- **Test naming**: `MethodName_Condition_ExpectedResult` (e.g., `TryMatchRoute_WhenPatternMatches_ReturnsTrue`).
- Add regression tests for bug fixes; do not remove tests without reason.

## Development Guides

Detailed standards in `docs/development/`:

| File | Covers |
|------|--------|
| `testing-strategy.md` | Unit vs integration vs E2E, mocking policy |
| `naming-conventions.md` | PascalCase/camelCase/`_camelCase` rules, suffixes |
| `dependency-injection.md` | Service lifetimes, registration style, interface usage |
| `tech-stack.md` | xUnit, Moq, Shouldly, YamlDotNet, `dotnet format` |
| `project-structure.md` | Layer responsibilities, feature-level organization |
| `error-handling.md` | Expected vs unexpected errors, centralized handler |
| `api-design.md` | REST conventions, thin controllers, DTOs |
| `configuration.md` | Options pattern, startup validation |
| `async-guidelines.md` | CancellationToken, no `.Result`/`.Wait()` |
| `logging.md` | Structured logging, log levels, no sensitive data |

## Language Rules

- All commit messages, PR titles/descriptions, and GitHub issues: English only.
- Follow Conventional Commits (`feat:`, `fix:`, `refactor:`, etc.).
- Inline code comments may be in Japanese when it improves clarity.
