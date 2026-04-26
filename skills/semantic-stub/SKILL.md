---
name: semantic-stub
description: >
  Use this skill when the user wants to inspect, debug, or manage a running SemanticStub
  server via MCP tools. Trigger whenever the user mentions SemanticStub, stub server,
  route matching, scenario state, or asks why a request matched or didn't match.
  Also trigger for questions like "which stub matches this request?",
  "show me recent requests", "check the scenario state", or "why did this response come back?".
license: MIT
compatibility: Requires a running SemanticStub server with MCP enabled (SEMANTIC_STUB_URL env var, default http://localhost:8080)
---

# SemanticStub MCP Skill

SemanticStub is a semantic-aware API mock server. This skill helps you use its MCP tools
efficiently — choosing the right tool for the task and minimizing unnecessary tool calls.

## Available tools

| Tool | Purpose |
|---|---|
| `get_config` | Active config snapshot (route count, semantic matching enabled, etc.) |
| `list_routes` | All active routes with routeId, path, method |
| `get_route` | Detail of one route (responses, conditional matches, scenario usage) |
| `get_scenarios` | Current scenario state snapshot |
| `get_metrics` | Aggregate metrics (request counts, latency, top routes) |
| `reset_metrics` | Reset aggregate metrics and recent request history |
| `get_requests` | Recent real request history (newest first) |
| `test_match` | Check which stub a virtual request matches — no side effects |
| `explain_match` | Full match explanation including semantic evaluation |
| `get_last_explain` | Explanation from the most recent real matched request |
| `reset_scenario_state` | Reset all scenarios or one named scenario to its initial state |
| `export_stubs_as_yaml` | Export recorded requests as draft OpenAPI 3.1 YAML stubs |
| `suggest_improvements` | Analyze a request and return actionable YAML improvement suggestions |

## Tool selection guide

### "Which stub matches this request?"
→ Use `test_match` directly. No need to call `list_routes` first unless the path is unknown.

### "Why did this request match / not match?"
→ Use `explain_match` for a virtual request, or `get_last_explain` for the most recent real request.
Do NOT chain multiple tools — `explain_match` already includes candidate evaluation.

### "What routes are registered?"
→ Use `list_routes`. If detail on one route is needed, follow up with `get_route`.

### "What state is the scenario in?"
→ Use `get_scenarios` directly. No other tools needed.

### "Show me recent requests" / "Were there any unmatched requests?"
→ Use `get_requests`. Default limit is 20. Increase if needed (max 100).
Look for items where `matchMode` is `null` or `failureReason` is non-null for unmatched requests.

### "How is the server performing?"
→ Use `get_metrics` for aggregate data. Use `get_requests` for per-request detail.

### "Reset metrics" / "Clear recent requests"
→ Use `reset_metrics`. It clears aggregate metrics and recent request history only.
It does not reset scenario state, reload active stub definitions, or clear `get_last_explain`.

### "Generate a stub definition from recent traffic"
→ Use `export_stubs_as_yaml`. Omit `index` to export all recent requests grouped by path/method, or provide `index` to export a single recorded request.
The output is a reviewable draft — fill in `TODO` placeholders before activating.
Call `get_requests` first if you need to identify which index to export.

### "Are my stub conditions good? Any ambiguous matches?"
→ Use `suggest_improvements`. Provide `index` to analyze a recorded request, or `method`+`path` for a virtual one.
Returns a list of improvement suggestions (`NoMatchFound`, `SemanticFallbackUsed`, `NoConditionsOnRoute`, `NearMissCandidate`) with YAML-oriented hints.

### General health check
→ Use `get_config` to confirm the server is running and check route count.

## Typical workflows

### Debug: request not matching as expected
1. `explain_match` with the exact method, path, and query/body
2. Read `matchedCandidate` and `failureReasons` in the response
3. Report why it failed and which candidate came closest

### Scenario debugging
1. `get_scenarios` to see current state
2. If state is wrong, use `reset_scenario_state` for all scenarios or the named scenario

### Quick status check
1. `get_config` → confirm running
2. `get_metrics` → check matched/unmatched ratio
3. `get_requests` → spot recent failures

### Generate stub drafts from recorded traffic
1. `get_requests` → identify relevant requests and their indices
2. `export_stubs_as_yaml` → export as YAML draft (omit `index` to export all, or provide `index` for one)
3. Present the YAML to the user and suggest filling in the `TODO` placeholders

### Improve existing stub definitions
1. `get_requests` → find a request that looks ambiguous (e.g. matched via semantic, or unmatched)
2. `suggest_improvements` with the request `index` → get improvement suggestions
3. Present the suggestions with YAML hints to the user

## Response format

- Always summarize results in Japanese when the user writes in Japanese
- For `get_requests`, highlight unmatched requests (failureReason is non-null)
- For `explain_match` / `test_match`, clearly state which stub matched and why
- For `get_scenarios`, show scenario name, current state, and whether it is active

## Notes

- `test_match` and `explain_match` do NOT mutate scenario state — safe to call anytime
- `reset_metrics` clears metrics and recent requests, but does not change scenario state, active stub definitions, or the latest real-request explanation
- `export_stubs_as_yaml` returns raw YAML text — always present it as a draft and remind the user to fill in `TODO` placeholders before activating
- `suggest_improvements` with `index` analyzes a recorded request; without `index`, provide both `method` and `path` for a virtual request
- All tools fail gracefully if the stub server is not running — inform the user and suggest starting it
