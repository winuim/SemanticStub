import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

const BASE_URL = process.env.SEMANTIC_STUB_URL ?? "http://localhost:8080";
// Keep the MCP layer thin by delegating to the existing HTTP inspection surface.
const RUNTIME = `${BASE_URL}/_semanticstub/runtime`;

// ─── API helper ───────────────────────────────────────────────────────────────

async function callApi(path: string, method = "GET", body?: unknown) {
  const res = await fetch(`${RUNTIME}${path}`, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  return res;
}

async function readJson(path: string, method = "GET", body?: unknown) {
  const res = await callApi(path, method, body);

  if (!res.ok) {
    throw new Error(await formatApiError(res));
  }

  return res.json();
}

async function postNoContent(path: string) {
  const res = await callApi(path, "POST");

  if (!res.ok) {
    throw new Error(await formatApiError(res));
  }
}

async function formatApiError(res: Response) {
  const text = await res.text();
  const suffix = text ? ` - ${text}` : "";
  return `SemanticStub API error: ${res.status} ${res.statusText}${suffix}`;
}

// Return JSON as plain text so tool-centric clients can display the full payload without custom parsing.
function toText(data: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }] };
}

// ─── Server ───────────────────────────────────────────────────────────────────

const server = new McpServer({
  name: "semantic-stub",
  version: "1.0.0",
});

server.registerTool(
  "get_config",
  {
    description:
      "Return metadata of the active configuration snapshot (timestamp, hash, route count, semantic matching enabled, etc.).",
    annotations: {
      readOnlyHint: true,
    },
  },
  async () => toText(await readJson("/config"))
);

server.registerTool(
  "list_routes",
  {
    description:
      "Return the list of active routes (routeId, path pattern, HTTP method, semantic matching usage, etc.). Use this first to get routeIds for other tools.",
    annotations: {
      readOnlyHint: true,
    },
  },
  async () => toText(await readJson("/routes"))
);

server.registerTool(
  "get_route",
  {
    description:
      "Return detailed information for a single route (responses, conditional match metadata, scenario usage, etc.).",
    inputSchema: {
      routeId: z.string().describe("Route ID obtained from list_routes."),
    },
    annotations: {
      readOnlyHint: true,
    },
  },
  async ({ routeId }) => toText(await readJson(`/routes/${encodeURIComponent(routeId)}`))
);

server.registerTool(
  "get_scenarios",
  {
    description:
      "Return the current scenario state snapshot (scenario name, current state, active flag). Use this to check which step a scenario is on.",
    annotations: {
      readOnlyHint: true,
    },
  },
  async () => toText(await readJson("/scenarios"))
);

server.registerTool(
  "get_metrics",
  {
    description:
      "Return process-local aggregate metrics (total requests, matched/unmatched counts, semantic counts, average latency, status code summary, top routes).",
    annotations: {
      readOnlyHint: true,
    },
  },
  async () => toText(await readJson("/metrics"))
);

server.registerTool(
  "reset_metrics",
  {
    description:
      "Reset process-local aggregate metrics and recent request history. Does not reset scenario state, reload active stub definitions, or clear the latest real-request explanation.",
    annotations: {
      readOnlyHint: false,
      destructiveHint: true,
      idempotentHint: true,
      openWorldHint: false,
    },
  },
  async () => {
    await postNoContent("/metrics/resets");

    return toText({
      operation: "reset_metrics",
      status: "ok",
      scope: "metrics_and_recent_requests",
    });
  }
);

server.registerTool(
  "get_requests",
  {
    description:
      "Return recent real request history (timestamp, method, path, routeId, status code, elapsed ms, match mode, failure reason). Newest first.",
    inputSchema: {
      limit: z
        .number()
        .int()
        .min(1)
        .max(100)
        .optional()
        .default(20)
        .describe("Number of records to return (default: 20, max: 100)."),
    },
    annotations: {
      readOnlyHint: true,
    },
  },
  async ({ limit }) => toText(await readJson(`/requests?limit=${limit}`))
);

server.registerTool(
  "test_match",
  {
    description:
      "Send a virtual request to check which stub matches. Does not send a real response or mutate scenario state.",
    inputSchema: {
      method: z.string().describe("HTTP method (GET, POST, PUT, DELETE, etc.)."),
      path: z.string().describe("Request path (e.g. /users)."),
      query: z
        .record(z.array(z.string()))
        .optional()
        .describe("Query parameters (e.g. { role: ['admin'] })."),
      headers: z
        .record(z.string())
        .optional()
        .describe("Request headers as key-value pairs."),
      // The .NET API expects the raw body string, not a structured JSON object.
      body: z
        .string()
        .optional()
        .describe("Raw request body string. JSON payloads should be stringified before calling the tool."),
      includeCandidates: z
        .boolean()
        .optional()
        .default(false)
        .describe("Whether to include detailed candidate evaluation in the response."),
      includeSemanticCandidates: z
        .boolean()
        .optional()
        .default(false)
        .describe("Whether to include semantic candidate scores when semantic matching is attempted."),
    },
    annotations: {
      readOnlyHint: true,
    },
  },
  async (args) => toText(await readJson("/test-match", "POST", args))
);

server.registerTool(
  "explain_match",
  {
    description:
      "Return structured match details for a virtual request, including deterministic and semantic evaluation.",
    inputSchema: {
      method: z.string().describe("HTTP method."),
      path: z.string().describe("Request path."),
      query: z
        .record(z.array(z.string()))
        .optional()
        .describe("Query parameters."),
      headers: z
        .record(z.string())
        .optional()
        .describe("Request headers."),
      // Keep the MCP input aligned with MatchRequestInfo.Body on the API side.
      body: z
        .string()
        .optional()
        .describe("Raw request body string. JSON payloads should be stringified before calling the tool."),
      includeCandidates: z
        .boolean()
        .optional()
        .default(true)
        .describe("Whether to include detailed candidate evaluation in the response."),
      includeSemanticCandidates: z
        .boolean()
        .optional()
        .default(false)
        .describe("Whether to include semantic candidate scores when semantic matching is attempted."),
    },
    annotations: {
      readOnlyHint: true,
    },
  },
  async (args) => toText(await readJson("/explain", "POST", args))
);

server.registerTool(
  "get_last_explain",
  {
    description:
      "Return the latest explanation captured from the most recent real matched request in the current process.",
    annotations: {
      readOnlyHint: true,
    },
  },
  async () => toText(await readJson("/explain/last"))
);

server.registerTool(
  "reset_scenario_state",
  {
    description:
      "Reset all scenarios, or one named scenario, back to the initial in-memory state already supported by SemanticStub.",
    inputSchema: {
      scenarioName: z
        .string()
        .trim()
        .min(1, "Scenario name must be a non-empty string.")
        .optional()
        .describe("Scenario name to reset. Omit to reset all configured scenarios."),
    },
    annotations: {
      readOnlyHint: false,
      destructiveHint: true,
      idempotentHint: true,
      openWorldHint: false,
    },
  },
  async ({ scenarioName }) => {
    // Reuse the existing reset endpoints so MCP does not redefine scenario mutation behavior.
    const hasScenarioName = scenarioName !== undefined;
    const path = hasScenarioName
      ? `/scenarios/${encodeURIComponent(scenarioName)}/resets`
      : "/scenarios/resets";

    await postNoContent(path);

    return toText({
      operation: "reset_scenario_state",
      status: "ok",
      scope: hasScenarioName ? "single" : "all",
      scenarioName: scenarioName ?? null,
    });
  }
);

server.registerTool(
  "export_stubs_as_yaml",
  {
    description:
      "Export recorded real requests as draft YAML stub definitions. " +
      "If `index` is provided, exports that single recorded request as a YAML draft. " +
      "Otherwise, exports the most recent `limit` requests grouped by path and method into a combined YAML document. " +
      "The output is a reviewable OpenAPI 3.1 draft — copy it into your stub YAML and fill in the TODO placeholders before activating.",
    inputSchema: {
      index: z
        .number()
        .int()
        .min(0)
        .optional()
        .describe(
          "Zero-based index into the recent request history (0 = most recent). " +
          "Omit to export all recent requests grouped into a single YAML document."
        ),
      limit: z
        .number()
        .int()
        .min(1)
        .max(100)
        .optional()
        .default(20)
        .describe("Number of recent requests to include when `index` is omitted (default: 20, max: 100)."),
    },
    annotations: {
      readOnlyHint: true,
    },
  },
  async ({ index, limit }) => {
    const path = index !== undefined
      ? `/requests/${index}/export/yaml`
      : `/requests/export/yaml?limit=${limit}`;

    const res = await callApi(path);

    if (!res.ok) {
      throw new Error(await formatApiError(res));
    }

    const yaml = await res.text();
    return { content: [{ type: "text" as const, text: yaml }] };
  }
);

server.registerTool(
  "suggest_improvements",
  {
    description:
      "Analyze a request against the active stub definitions and return actionable YAML improvement suggestions. " +
      "Detects ambiguous or low-quality matches such as semantic fallback usage, missing x-match conditions, " +
      "near-miss candidates, and undefined routes. " +
      "If `index` is provided, the recorded real request at that index is analyzed. " +
      "Otherwise, a virtual request is constructed from the supplied `method` and `path` fields.",
    inputSchema: {
      index: z
        .number()
        .int()
        .min(0)
        .optional()
        .describe(
          "Zero-based index into the recent request history (0 = most recent). " +
          "When provided, analyzes that recorded request. Omit to analyze a virtual request instead."
        ),
      method: z
        .string()
        .optional()
        .describe("HTTP method for the virtual request (required when `index` is omitted)."),
      path: z
        .string()
        .optional()
        .describe("Request path for the virtual request (required when `index` is omitted)."),
      query: z
        .record(z.array(z.string()))
        .optional()
        .describe("Query parameters for the virtual request."),
      headers: z
        .record(z.string())
        .optional()
        .describe("Request headers for the virtual request."),
      body: z
        .string()
        .optional()
        .describe("Raw request body string for the virtual request. JSON payloads should be stringified first."),
    },
    annotations: {
      readOnlyHint: true,
    },
  },
  async ({ index, method, path, query, headers, body }) => {
    if (index !== undefined) {
      return toText(await readJson(`/requests/${index}/suggest-improvements`));
    }

    if (!method || !path) {
      throw new Error("Either `index` or both `method` and `path` must be provided.");
    }

    return toText(
      await readJson("/suggest-improvements", "POST", { method, path, query, headers, body })
    );
  }
);

// ─── Start ────────────────────────────────────────────────────────────────────

const transport = new StdioServerTransport();
await server.connect(transport);
