# Code Review Policy

## Objective
Provide high-signal code review comments only.
Avoid noise, speculation, and low-confidence feedback.

---

## Findings (Strict)
Only include issues that meet ALL of the following:

- There is clear evidence in the code or diff
- The issue has meaningful impact (bug, regression, security, data issue, or maintainability risk)
- The failure scenario can be explained concretely
- A realistic fix can be suggested

If any of the above is not satisfied, DO NOT include it as a finding.

---

## Do NOT report
- Speculative concerns ("might", "could", "possibly")
- Style preferences without impact
- Optional refactoring suggestions
- Issues that depend on missing context
- Comments made just to increase coverage

---

## Confidence Rule (Critical)
Only report issues when confidence is HIGH.

- If unsure → DO NOT report as a finding
- If additional confirmation is needed → put it under "Questions / Things to verify"

Returning "no findings" is acceptable and preferred over low-confidence comments.

---

## Output Format

### Findings
For each finding, include:

- Severity: Critical | High | Medium
- Problem: What is wrong
- Impact: Why it matters
- Evidence: What in the code suggests this
- Fix: Concrete suggestion

---

### Questions / Things to verify
- Only include important but uncertain concerns
- Do NOT mix with Findings

---

## Review Style
- Prefer 0–3 strong findings over many weak ones
- Be concise and specific
- Avoid repeating the same issue
- Focus on correctness, security, and unintended behavior changes
