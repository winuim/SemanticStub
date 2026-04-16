

## Logging

This project uses logging to support observability, troubleshooting, and operational clarity without adding unnecessary noise.

### General Principles
- Log important events, warnings, and errors.
- Prefer structured logging over interpolated or concatenated log messages.
- Keep logs clear, consistent, and useful.
- Avoid excessive logging in hot paths or frequently executed code.
- Prefer logs that explain what happened and why.

### What to Log
Log when it helps diagnose behavior, failures, or significant system events.

Examples:
- application startup and shutdown
- configuration loading results
- request handling milestones when useful
- scenario state transitions
- external dependency failures
- unexpected exceptions
- important warnings or fallback behavior

### What Not to Log
- Do not log sensitive information.
- Do not log secrets, credentials, tokens, or private keys.
- Do not log full request or response bodies unless clearly justified and safe.
- Do not add noisy logs for trivial internal steps.

### Structured Logging
- Prefer structured log parameters instead of embedding values directly into strings.
- Include relevant identifiers and context when useful.

Examples of useful context:
- route
- scenario name
- request path
- request id / trace id
- response status code
- external dependency name

- Include identifiers (e.g., request id, scenario name) to correlate logs.

### Log Levels
Use log levels intentionally:

- `Trace` → very detailed diagnostic information
- `Debug` → development-focused diagnostic details
- `Information` → normal important application events
- `Warning` → unexpected but recoverable situations
- `Error` → failures that affect the current operation
- `Critical` → severe failures affecting application stability

### Exception Logging
- Log unexpected exceptions.
- Do not log and rethrow the same exception repeatedly without adding value.
- When catching exceptions, add useful context only when it improves diagnosis.

### API and Request Logging
- Log request-related information at an appropriate level.
- Avoid duplicating logs that are already captured by middleware or framework-level logging.
- Keep controller-level logging minimal when the same information is already available elsewhere.
- Avoid logging the same event at multiple layers.

### Performance Considerations
- Avoid excessive logging inside tight loops or frequently executed match paths.
- Be careful with large object serialization in logs.
- Prefer concise, high-value log events.

### Consistency
- Use consistent message patterns across the codebase.
- Follow existing logging conventions when extending functionality.
- Keep log messages readable and action-oriented.

### Design Goals
- Useful operational visibility
- Clear diagnostics for failures
- Minimal unnecessary noise
- Safe handling of sensitive information