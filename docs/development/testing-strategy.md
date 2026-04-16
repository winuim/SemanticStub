## Test Strategy

### Principles
- Cover business logic with unit tests.
- Test API endpoints with integration tests using `WebApplicationFactory`.
- Limit end-to-end tests to major user scenarios.
- Use mocks only for external dependencies that already have interfaces.

### Unit Tests
Use unit tests as the default for domain logic, matching rules, scenario transitions, route compilation, configuration handling, and other business logic.

Guidelines:
- Unit tests should be fast and deterministic.
- Prefer testing behavior through public APIs.
- Avoid unnecessary mocking of internal collaborators.
- Add regression tests for bug fixes.
- Cover edge cases and failure cases when they affect observable behavior.
- Avoid testing private methods directly; test through public behavior.
- Keep tests independent; they should not rely on execution order.

Examples of code that should usually be covered by unit tests:
- matchers
- route/template parsing and compilation
- scenario state transitions
- configuration merge logic
- response selection logic
- helper classes with meaningful logic

### Integration Tests
Use integration tests for HTTP pipeline behavior and API endpoint verification.

Guidelines:
- Use `WebApplicationFactory` for endpoint tests.
- Verify routing, model binding, filters, middleware interaction, status codes, and response shapes.
- Prefer real application wiring unless isolation is required for external systems.
- Replace only truly external dependencies when needed.
- Use realistic configuration and data where possible.
- Avoid excessive mocking in integration tests.

Examples of code that should usually be covered by integration tests:
- admin or inspection endpoints
- stub execution through HTTP requests
- middleware behavior
- serialization/deserialization behavior
- DI wiring that affects runtime behavior

### End-to-End Tests
Use end-to-end tests only for major scenarios that validate the system as a whole.

Guidelines:
- Keep the number of E2E tests small.
- Focus on critical flows that provide confidence across multiple layers.
- Do not use E2E tests as a substitute for unit or integration coverage.

Examples:
- loading stub definitions and serving responses end-to-end
- scenario progression across multiple requests
- semantic matching flow when enabled in a realistic environment

### Mocking Policy
Use mocks only for external dependencies that already have interfaces.

Guidelines:
- Mock external systems such as API clients, storage clients, clock abstractions, or other process boundaries when needed.
- Do not introduce interfaces only to make mocking easier unless the abstraction is justified by design.
- Prefer real implementations for simple internal services.
- Prefer fakes or test doubles over deep mock setups when they make tests easier to understand.

### Coverage Expectations
- Business logic should be covered primarily by unit tests.
- API behavior should be covered by integration tests.
- Critical user flows should have selective end-to-end coverage.
- Bug fixes should include regression tests when practical.

### Test Data

- Use clear and minimal test data focused on the behavior being tested.
- Avoid large or overly complex test fixtures.
- Prefer inline test data when it improves readability.
- Use builders or helpers when setup becomes repetitive.

### Test Design Guidelines
- Write tests that describe behavior clearly.
- Keep each test focused on one behavior.
- Avoid brittle assertions tied to incidental implementation details.
- Name tests so the intended behavior is easy to understand.
- Prefer readable test setup over overly clever reuse.
- Prefer Arrange-Act-Assert structure.
- Avoid excessive assertions in a single test.
- Tests should fail for a single clear reason.

### Preferred Libraries
- xUnit
- Moq
- Shouldly

### Naming Conventions for Tests

- Follow naming patterns defined in `naming-conventions.md`.
- Test names should clearly describe behavior and expected outcome.
- Avoid vague test names such as `Test1` or `ShouldWork`.
