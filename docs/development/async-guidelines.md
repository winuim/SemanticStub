## Async Guidelines

This project uses async/await to handle I/O-bound operations efficiently while keeping code clear and maintainable.

### General Principles
- Use async/await for I/O-bound operations.
- Keep asynchronous code simple and easy to follow.
- Avoid unnecessary async usage for purely CPU-bound logic.

### When to Use Async
Use async/await when dealing with:
- HTTP calls
- File I/O
- Database access
- External services

Do not use async when:
- The operation is purely in-memory and fast
- There is no real asynchronous work

### Avoid Blocking Calls
- Do not use `.Result` or `.Wait()` on tasks.
- Avoid blocking the thread in asynchronous flows.
- Do not mix synchronous and asynchronous calls within the same execution flow.

### Method Design
- Asynchronous methods should return `Task` or `Task<T>`.
- Avoid `async void` except for event handlers.
- Use clear method names when helpful (e.g., `GetStubAsync`).

### Cancellation
- Accept `CancellationToken` in asynchronous methods when appropriate.
- Pass the token through to downstream async calls.
- Respect cancellation requests when it is safe to do so.

### Error Handling
- Let exceptions propagate naturally in async methods.
- Avoid wrapping async code in unnecessary try-catch blocks.
- Handle exceptions only when you can add meaningful context or recovery.

### Parallelism
- Use parallelism carefully.
- Avoid starting multiple tasks without awaiting them.
- Prefer `Task.WhenAll` when running multiple independent async operations.

### ConfigureAwait
- Use `ConfigureAwait(false)` in library or reusable code if needed.
- It is generally not required in ASP.NET Core application code.

### Performance Considerations
- Avoid excessive task allocations in hot paths.
- Do not over-parallelize small operations.
- Keep async flows simple and predictable.

### Consistency
- Follow existing async patterns in the codebase.
- Avoid mixing synchronous and asynchronous styles unnecessarily.

### Design Goals
- Efficient use of asynchronous I/O
- Clear and maintainable async code
- Avoidance of deadlocks and blocking
- Predictable execution behavior