

## Project Structure

This project follows a layered structure to keep responsibilities clear and maintainable.

### Overview
The solution is organized into logical layers:

- API / Web → HTTP entry point
- Application → business logic and orchestration
- Infrastructure → external systems and I/O
- Domain (optional) → core domain models and logic

### Directory Layout

Example structure:

```
/src
  /SemanticStub.Api
  /SemanticStub.Application
  /SemanticStub.Infrastructure
  /SemanticStub.Domain (optional)

/tests
  /SemanticStub.Api.Tests
  /SemanticStub.Application.Tests
  /SemanticStub.Infrastructure.Tests
```

### Layer Responsibilities

#### API / Web
- Handles HTTP requests and responses
- Contains controllers, middleware, filters
- Performs request validation and response shaping
- Should not contain business logic

#### Application
- Contains business logic and orchestration
- Coordinates between components
- Implements use cases and workflows
- Depends on abstractions (interfaces), not concrete infrastructure

#### Infrastructure
- Implements external dependencies
- Examples:
  - HTTP clients
  - file system access
  - persistence
  - external APIs
- Contains concrete implementations of interfaces defined in Application

#### Domain (Optional)
- Contains core domain models and rules
- Should be independent of frameworks
- Used when domain complexity justifies separation

### Dependency Rules

- API can depend on Application
- Application can depend on Domain
- Infrastructure can depend on Application
- Domain should not depend on other layers

Disallowed:
- Application must not depend on Infrastructure implementations
- Domain must not depend on Application or Infrastructure

### Test Project Structure

- Each main project should have a corresponding test project
- Test projects should mirror the structure of the target project where practical

Examples:
- `SemanticStub.Application.Tests`
- `SemanticStub.Infrastructure.Tests`

Guidelines:
- Unit tests primarily target Application and Domain
- Integration tests target API and Infrastructure behavior

### File Organization

- Group files by feature or responsibility rather than by technical type when appropriate
- Avoid overly deep folder nesting
- Keep related classes close together

Examples:
- Route-related logic grouped together
- Scenario-related logic grouped together

### Naming Conventions for Structure

- Project names should follow `SemanticStub.<Layer>` pattern
- Test projects should follow `SemanticStub.<Layer>.Tests`

### Adding New Code

- Place new code in the appropriate layer based on responsibility
- Do not introduce cross-layer dependencies that violate the rules
- Follow existing folder patterns and naming conventions

### Design Goals

- Clear separation of concerns
- Maintainable and scalable structure
- Easy navigation for both humans and tools
- Predictable placement of new code