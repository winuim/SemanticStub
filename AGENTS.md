# SemanticStub

SemanticStub is a **.NET 10** project and an **OpenAPI-based stub server**.

## Contract and matching requirements
- Use **OpenAPI 3.1** as the base API contract.
- Keep API definitions compliant with OpenAPI where possible.
- Implement custom behavior with `x-stub-*` extensions.
- Support **strict** and **hybrid** request matching modes.
- **Strict mode** validates method, path, content-type, and request schema.
- **Hybrid mode** keeps structure strict but allows flexible matching on selected request body fields.

## Architecture
- Keep the architecture modular and easy to extend.
- Prefer simple, readable C# code.
- Avoid over-engineering in early versions.

## Project structure
- `SemanticStub.Core`: shared models and abstractions.
- `SemanticStub.OpenApi`: OpenAPI parsing and mapping.
- `SemanticStub.Matching`: matching logic.
- `SemanticStub.Api`: ASP.NET Core hosting.

## Development guidelines
- Keep responsibilities separated by project.
- Use `Directory.Packages.props` for central package management.
- Write tests for parsing, matching, and API behavior.
- Prefer incremental implementation.
