# AGENTS.md

## Priorities
1. Preserve existing behavior and YAML compatibility
2. Make the smallest safe change
3. Avoid unrelated edits
4. Prefer readability over cleverness

## Core Rules
- Do not break existing YAML compatibility
- Do not change working behavior unless explicitly required
- Do not refactor unrelated code (e.g. renaming classes or reorganizing files)
- Do not rename or move files unless explicitly requested
- Do not add dependencies unless necessary

## Task Patterns

Refer to SKILLS.md for reusable task patterns.
When a task name is specified, follow the corresponding skill.

## Architecture Constraints
- Preserve existing structure (Controller / Service / Infrastructure)
- Do not move logic across layers
- Do not introduce new abstractions without clear need

## YAML Rules
- Use OpenAPI 3.1 structure
- Use `x-*` for all custom extensions
- Do not rename OpenAPI fields
- Do not introduce alternative YAML structures
- YAML is the source of truth
- Keep YAML predictable and human-readable

## Working Style
- Identify the smallest relevant set of files first
- Prefer interfaces, tests, and docs before implementations
- Do not scan the whole repository unless needed
- Explain assumptions when requirements are unclear
- Choose the safest implementation when uncertain

## Code Rules
- Keep methods small and focused
- Keep classes single-purpose
- Use clear and descriptive names
- Add XML docs for public classes and public methods
- Comments should explain why, not what
- Avoid redundant or trivial comments

## Testing
- Add tests for behavior changes
- Add regression tests for bug fixes
- Prefer unit tests
- Do not remove tests without reason

## Approval Required

Ask before:
- breaking YAML compatibility
- changing routing behavior
- changing API contracts
- adding new abstractions without clear need
- introducing external services

## Language Rules
- All commit messages must be written in English
- All pull request titles and descriptions must be written in English
- All GitHub issue titles and descriptions must be written in English
- Follow Conventional Commits in English (e.g. feat:, fix:, refactor:)
- Inline code comments may be written in Japanese if it improves clarity
- Do not mix Japanese and English in the same commit message or PR description

## Development Guides

Follow the detailed development guidelines in the following documents when relevant:

- docs/development/testing-strategy.md
- docs/development/naming-conventions.md
- docs/development/dependency-injection.md
- docs/development/tech-stack.md
- docs/development/project-structure.md
- docs/development/error-handling.md
- docs/development/logging.md
- docs/development/api-design.md
- docs/development/configuration.md
- docs/development/async-guidelines.md

These documents define project-specific standards for testing, naming, dependency injection, technology choices, and structure. Always follow them when making related changes. If multiple guidelines apply, prioritize correctness, safety, and consistency.
