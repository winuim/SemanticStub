## API Design

This project follows consistent API design principles to ensure clarity, predictability, and maintainability.

### General Principles
- Keep APIs simple and predictable.
- Follow RESTful conventions where appropriate.
- Design APIs for clarity over cleverness.
- Maintain consistency across all endpoints.

### Routing
- Use clear and consistent route naming.
- Prefer nouns over verbs in route paths.
- Use plural resource names when appropriate.

Examples:
- `/stubs`
- `/scenarios`
- `/metrics`

- Avoid deeply nested routes unless necessary.
- Use route parameters for resource identification.

Examples:
- `/stubs/{id}`
- `/scenarios/{scenarioName}`

- Prefer explicit endpoints for operations instead of overloading a single endpoint.

### HTTP Methods
Use HTTP methods consistently:

- `GET` → retrieve data
- `POST` → create or trigger processing
- `PUT` → replace/update a resource
- `PATCH` → partial update
- `DELETE` → remove a resource

Avoid using incorrect methods for convenience.

### Status Codes
Return appropriate HTTP status codes:

- `200 OK` → successful request
- `201 Created` → resource created
- `204 No Content` → successful with no response body
- `400 Bad Request` → validation or client error
- `404 Not Found` → resource not found
- `500 Internal Server Error` → unexpected error
- Do not return 200 OK for error cases.

- Use consistent status codes across endpoints.

### Request and Response Models
- Use DTOs for request and response models.
- Do not expose internal domain models directly.
- Keep request and response shapes simple and explicit.

Examples:
- `CreateStubRequest`
- `StubResponse`

### Controllers
- Controllers should be thin.
- Do not implement business logic in controllers.
- Delegate logic to application services.
- Focus on request handling, validation, and response formatting.

### Validation
- Validate inputs at the API boundary.
- Return clear and consistent validation errors.
- Avoid relying on exceptions for validation flow.

### Error Responses
- Return consistent error response formats.
- Do not expose internal exception details.
- Use appropriate status codes for expected errors.
- Use a consistent error response structure (e.g., code, message).

### Consistency
- Use consistent naming across routes, parameters, and DTOs.
- Follow existing API patterns when adding new endpoints.
- Avoid introducing conflicting styles.

### Versioning (Optional)
- Introduce API versioning when breaking changes are required.
- Keep versioning strategy consistent once introduced.

### Design Goals
- Predictable API behavior
- Clear and consistent structure
- Separation of concerns (API vs business logic)
- Ease of use for clients