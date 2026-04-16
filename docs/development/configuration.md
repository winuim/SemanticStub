## Configuration

This project uses a consistent and maintainable approach to configuration management.

### General Principles
- Use strongly-typed configuration via Options classes.
- Avoid accessing `IConfiguration` directly in application or domain services.
- Keep configuration simple, explicit, and predictable.
- Centralize configuration binding and validation.

### Options Pattern
- Use the Options pattern (`IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>`) for configuration.
- Group related settings into a single Options class.
- Name configuration classes with the `Options` suffix.

Examples:
- `StubOptions`
- `MatchingOptions`

### Binding and Registration
- Bind configuration in a single place (e.g., in `Program.cs` or extension methods).
- Avoid scattering configuration binding across multiple files.
- Validate configuration at startup when possible.

Example:
- `services.Configure<StubOptions>(configuration.GetSection("Stub"))`

### Accessing Configuration
- Inject strongly-typed Options into services.
- Do not inject `IConfiguration` into business logic.

Preferred:
- `IOptions<StubOptions>`

Avoid:
- Direct calls to `IConfiguration["SomeKey"]` inside services

- Do not pass configuration values through multiple layers unnecessarily.

### Validation
- Validate configuration values during application startup.
- Fail fast if critical configuration is missing or invalid.
- Use data annotations or custom validation when appropriate.

### Environment-Specific Configuration
- Use environment-specific configuration files when needed.
  - e.g., `appsettings.Development.json`
- Keep environment overrides minimal and explicit.

### Secrets Management
- Do not store secrets in source-controlled configuration files.
- Use environment variables or external secret stores.
- Ensure sensitive values are not logged.

### Defaults
- Provide sensible default values where appropriate.
- Avoid hidden or implicit defaults that are difficult to trace.

### Consistency
- Use consistent naming for configuration keys and sections.
- Follow existing patterns when adding new configuration.

### Design Goals
- Clear and maintainable configuration structure
- Strong typing and validation
- Safe handling of sensitive data
- Predictable configuration behavior