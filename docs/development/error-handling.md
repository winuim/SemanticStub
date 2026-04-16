

## Error Handling

This project defines a consistent approach to handling errors to ensure reliability, clarity, and maintainability.

### General Principles
- Do not swallow exceptions silently.
- Handle only exceptions you can meaningfully respond to.
- Let unexpected exceptions propagate to a centralized handler.
- Keep error handling simple and predictable.
- Do not use exceptions for normal control flow.

### Expected vs Unexpected Errors

#### Expected Errors
- Represent known failure conditions (e.g., validation failures, not found, invalid state).
- Should be handled explicitly.
- Should result in appropriate responses (e.g., 400, 404).

#### Unexpected Errors
- Represent bugs or system failures.
- Should not be handled locally unless necessary.
- Should be logged and handled by a global exception handler.

### API Layer Behavior
- Return appropriate HTTP status codes.
- Do not expose internal exception details to clients.
- Use clear and consistent error response formats.
- Keep controllers thin; avoid complex error handling logic in controllers.
- Map domain-specific exceptions to appropriate HTTP status codes in a centralized handler.

### Exception Design
- Use specific exception types when they add clarity.
- Prefer domain-specific exceptions for business logic errors.
- Avoid overusing custom exceptions without clear purpose.

Examples:
- `RouteNotFoundException`
- `InvalidScenarioStateException`

### Logging
- Log unexpected exceptions.
- Include useful context when logging (e.g., route, request data, identifiers).
- Do not log sensitive information.

### When to Catch Exceptions
Catch exceptions only when you can:
- Recover from the error
- Translate the error into a meaningful domain or API response
- Add useful context before rethrowing

Otherwise, allow the exception to propagate.

### Rethrowing Exceptions
- Preserve the original stack trace when rethrowing.
- Do not use `throw ex;`
- Use `throw;` to rethrow the original exception.

### Validation
- Prefer validating inputs early rather than relying on exceptions.
- Use clear validation errors for client mistakes.

### Design Goals
- Predictable error handling behavior
- Clear separation between expected and unexpected failures
- Minimal duplication of error handling logic
- Safe and secure error responses