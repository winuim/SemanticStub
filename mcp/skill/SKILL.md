---
name: semantic-stub
description: >
  Use this skill when the user wants to inspect, debug, or manage a running SemanticStub
  server via MCP tools. Trigger whenever the user mentions SemanticStub, stub server,
  route matching, scenario state, or asks why a request matched or didn't match.
  Also trigger for questions like "which stub matches this request?",
  "show me recent requests", "check the scenario state", or "why did this response come back?".
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
| `get_requests` | Recent real request history (newest first) |
| `test_match` | Check which stub a virtual request matches — no side effects |
| `explain_match` | Full match explanation including semantic evaluation |
| `get_last_explain` | Explanation from the most recent real matched request |

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

### General health check
→ Use `get_config` to confirm the server is running and check route count.

## Typical workflows

### Debug: request not matching as expected
1. `explain_match` with the exact method, path, and query/body
2. Read `matchedCandidate` and `failureReasons` in the response
3. Report why it failed and which candidate came closest

### Scenario debugging
1. `get_scenarios` to see current state
2. If state is wrong, suggest restarting the stub server to reset (no reset API yet)

### Quick status check
1. `get_config` → confirm running
2. `get_metrics` → check matched/unmatched ratio
3. `get_requests` → spot recent failures

## Response format

- Always summarize results in Japanese when the user writes in Japanese
- For `get_requests`, highlight unmatched requests (failureReason is non-null)
- For `explain_match` / `test_match`, clearly state which stub matched and why
- For `get_scenarios`, show scenario name, current state, and whether it is active

## Notes

- `test_match` and `explain_match` do NOT mutate scenario state — safe to call anytime
- Scenario state resets on stub server restart
- All tools fail gracefully if the stub server is not running — inform the user and suggest starting it
