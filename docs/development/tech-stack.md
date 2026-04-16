## Tech Stack

This project uses a modern, pragmatic .NET-based technology stack. The goal is to prefer stable, well-supported, and widely adopted libraries while avoiding unnecessary complexity.

### Core Platform
- .NET (latest stable LTS or current version)
- ASP.NET Core

### Testing
- xUnit → test framework
- Moq → mocking library
- Shouldly → assertion library

### Configuration / Serialization
- System.Text.Json → default JSON serialization
- YamlDotNet → YAML parsing and configuration

### Dependency Injection
- Microsoft.Extensions.DependencyInjection (built-in)

### Logging
- Microsoft.Extensions.Logging (baseline)
- Optional: Serilog (for structured logging when needed)


### HTTP / Networking
- HttpClient via IHttpClientFactory

### API Documentation
- Swagger / OpenAPI (Swashbuckle) → API documentation and testing

### Validation
- Built-in model validation (DataAnnotations)
- Optional: FluentValidation

### Resilience (Optional)
- Polly → retry, circuit breaker, timeout handling

### Utilities
- Time abstraction (e.g., IClock) when needed for testing

### Development Tools
- Visual Studio / VS Code
- Git + GitHub

### Guidelines for Adding Libraries
- Prefer built-in .NET libraries when sufficient.
- Choose widely adopted and actively maintained libraries.
- Avoid adding libraries for trivial functionality.
- Avoid introducing multiple libraries that solve the same problem.
- Ensure new dependencies provide clear value (performance, readability, maintainability, or capability).

### Versioning Strategy
- Prefer stable releases over preview versions.
- Keep dependencies reasonably up to date.
- Avoid unnecessary upgrades that do not provide clear benefits.

### Notes
- This project prioritizes simplicity and maintainability over trend-driven choices.
- New technologies can be adopted when they provide clear, practical benefits.
