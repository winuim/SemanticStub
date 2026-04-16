## Naming Conventions

This project follows Microsoft C# naming conventions as the baseline, with a few project-specific rules to improve consistency and readability.

### General Rules
- Use clear, descriptive, intention-revealing names.
- Prefer full words over unclear abbreviations.
- Avoid ambiguous or overly generic names such as `data`, `value`, or `item`.
- Keep naming consistent across the codebase.

### Casing Rules
- Use PascalCase for:
  - Classes, records, structs, enums, delegates
  - Public and protected members
  - Namespaces
- Use camelCase for:
  - Method parameters
  - Local variables
- Use `_camelCase` for private fields.

### Type Naming
- Class, record, struct, enum, and delegate names use PascalCase.
- Interface names:
  - Must start with `I`
  - Use PascalCase

Examples:
- `StubConfiguration`
- `RouteMatcher`
- `ISemanticMatcher`

### Member Naming
- Methods use PascalCase and should be verbs or verb phrases.
- Properties use PascalCase and should be nouns or adjectives.
- Boolean members should read naturally (e.g., `IsEnabled`, `HasRoutes`).

Examples:
- `GetEffectiveConfiguration()`
- `TryMatchRoute()`
- `IsEnabled`
- `HasScenarioState`

### Field and Variable Naming
- Private fields use `_camelCase`.
- Local variables and parameters use camelCase.
- Avoid one-letter variable names except for trivial loop counters.

Examples:
- `_logger`
- `_routeTable`
- `requestPath`
- `matchedRoute`

### Test Naming
- Test class names should match the target type and end with `Tests`.
- Test method names should clearly describe behavior.
- Prefer the pattern:
  - `MethodName_Condition_ExpectedResult`

Examples:
- `TryMatchRoute_WhenPatternMatches_ReturnsTrue`
- `Next_WhenScenarioHasNoResponses_ReturnsNull`

### Common Suffixes
Use consistent suffixes when they improve clarity:

- `Options` → configuration classes used with DI/config binding
- `Request` / `Response` → API models
- `Exception` → custom exception types
- `Provider`, `Factory`, `Service` → when they reflect real responsibilities

### Abbreviations
- Prefer full words unless the abbreviation is widely accepted.
- Use standard .NET-style abbreviations:
  - `Id` (not `ID`)
  - `Http`, `Url`, `Xml`

### Interfaces and Abstraction
- Do not introduce interfaces solely for naming symmetry.
- Only create interfaces when they provide real architectural or testing value.

### Consistency
- Follow existing naming patterns in the codebase when modifying or extending functionality.
- Avoid introducing new naming styles that conflict with established conventions.
