

## Dependency Injection

This project uses dependency injection to keep composition explicit, support testing where appropriate, and maintain a clear separation between application code and external dependencies.

### Interface Usage
- External dependencies must be abstracted behind interfaces.
  - Examples: database access, HTTP clients, email sending, file storage, message brokers, clock abstractions
- Dependencies that need to be mocked in unit tests should be represented by interfaces.
- Models, DTOs, and simple data containers should not be abstracted behind interfaces.
- Do not introduce interfaces for every class by default.
- Do not add interfaces solely for naming symmetry or speculative future reuse.

### When to Introduce an Interface
Introduce an interface when at least one of the following is true:
- The implementation depends on an external system or process boundary.
- The dependency is expected to be replaced in unit tests.
- Multiple implementations are meaningful in the design.
- The abstraction improves separation between layers.

Do not introduce an interface when:
- The type is a model, DTO, options class, or simple value holder.
- The class is an internal implementation detail with no real abstraction value.
- The only reason is to satisfy a blanket rule that every service must have an interface.

### Service Lifetimes
- Use `Scoped` by default.
- Use `Singleton` for stateless shared services that are safe to reuse across the application lifetime.
- Use `Transient` for lightweight, short-lived processing components.
- Do not inject `Scoped` services into `Singleton` services.
  - Avoid captive dependencies.

### Lifetime Guidelines
#### Scoped
Use `Scoped` for most application services, especially when:
- they participate in request-level work
- they depend on other scoped services
- per-request consistency is desirable

#### Singleton
Use `Singleton` only when the service:
- is stateless or safely shared
- does not depend on scoped services
- is designed for concurrent use

Examples:
- configuration readers with immutable state
- reusable registries or caches when appropriate

#### Transient
Use `Transient` for:
- lightweight helpers
- short-lived processors
- objects that do not need shared state

Do not use `Transient` by default without a reason.

### Registration Style
- Keep `Program.cs` focused on application composition and startup flow.
- Do not place large numbers of individual service registrations directly in `Program.cs`.
- Group registrations into extension methods.
- Organize registration methods by layer or responsibility.

Examples:
- `AddApplicationServices()`
- `AddInfrastructureServices()`
- `AddApiServices()`

### Layered Registration
Prefer separating registration by layer, such as:
- Application
- Infrastructure
- API / Web

This keeps dependencies explicit and makes startup code easier to maintain.

### Testing Considerations
- Prefer mocking external dependencies rather than internal business logic.
- Do not introduce extra interfaces solely to satisfy mocking preferences.
- Prefer real implementations for simple internal services when that keeps tests clearer.

### Design Principles
- Prefer explicit, purposeful abstractions.
- Keep dependency graphs simple.
- Choose lifetimes intentionally.
- Avoid over-abstraction.
- Follow existing project patterns when extending DI configuration.