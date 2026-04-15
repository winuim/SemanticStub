# SKILLS

These are reusable task patterns.
When a task name is mentioned, follow the corresponding instructions.

---

## run-issue-end-to-end

### Goal
Execute the full development workflow for an issue from planning to merge.

### Workflow
1. Run `plan-issue`
2. Run `implement-issue`
3. Run `review`
4. Run `create-pr`

5. **Pause and wait for review feedback**
   - Do NOT proceed automatically beyond this point
   - Resume only when explicit instruction or review comments are provided

6. While review comments exist:
   - Run `plan-review-response`
   - Run `implement-review-response`
   - Run `update-pr-after-review`

   - **Pause and wait for re-review feedback**
     - Do NOT proceed automatically beyond this point
     - Resume only when explicit instruction or further comments are provided

   - Stop looping when all review comments are resolved or explicit approval is given

7. Run `merge`

### Constraints
- Follow AGENTS.md
- Do not skip steps unless explicitly instructed
- Ensure each step completes successfully before moving to the next
- MUST pause after `create-pr` and wait for external input before continuing
- MUST pause after `update-pr-after-review` and wait for re-review before continuing
- MUST continue the review loop until all comments are resolved or explicit approval is given, then proceed to `merge`

### Decision Handling
- If critical decisions are required, pause and ask for confirmation before proceeding.

Critical decisions include:
- Ambiguous requirements not clearly defined in the issue
- Multiple implementation approaches with different trade-offs
- Changes affecting YAML schema, API behavior, or external contracts
- Unclear test expectations

- For non-critical implementation details, proceed autonomously.

### Output
- Summary of each step execution
- Final merge result

---

## plan-issue

### Goal
Understand a GitHub Issue and produce an implementation plan WITHOUT making any code changes.

### Workflow
1. Retrieve and read the GitHub Issue
2. Summarize the issue
3. Identify the minimal set of relevant files
4. Propose a change strategy
5. Analyze risks and impact
6. Define a test strategy
7. List assumptions and unknowns

### Constraints
- Do NOT implement code
- Do NOT modify files
- Do NOT create commits or PRs
- Follow AGENTS.md
- Preserve YAML compatibility and existing behavior
- Limit scope to the smallest necessary surface

### Output format

## Summary
## Files to Inspect
## Planned Changes
## Risks / Impact
## Tests
## Assumptions

### Decision Handling
- If the issue is ambiguous or important assumptions are required, pause and ask for confirmation before finalizing the plan.
- For minor implementation details, note the assumption and proceed.

---

## implement-issue

### Goal
Implement the approved plan while preserving existing behavior, YAML compatibility, and the current architecture.

### Workflow
1. Follow the previously approved plan
2. Make only the minimum required code changes
3. Add or update only the tests required for the change
4. Verify the issue requirements are satisfied
5. Summarize the implementation
6. List all changed files
7. Report executed tests and results
8. Record remaining notes, assumptions, or follow-up items

### Constraints
- Follow AGENTS.md
- Follow the previously presented plan
- Satisfy the issue requirements
- Preserve existing behavior and YAML compatibility
- Keep changes minimal
- Do not make unrelated refactors
- Maintain the existing architecture (Controller / Service / Infrastructure)
- Do NOT create commits or PRs
- MUST pause if no approved plan is available
- MUST pause if implementation would require deviating from the approved plan

### Output format

## Summary
## Files Changed
## Tests
## Notes

---

## review

### Goal
Review code quality without modifying code.

### Checklist
- Readability
- Naming
- Unnecessary code
- Test coverage
- Potential bugs
- Security concerns

### Constraints
- Do NOT modify code
- Follow AGENTS.md

### Output format
- List issues or say "No issues found"

---

## create-pr

### Goal
Create a review-ready pull request for the implemented changes.

### Workflow
1. Ensure implementation is complete and verified
2. Remove any unrelated or accidental changes
3. Group changes into the minimal necessary commits
4. Write commit message in Conventional Commits format
5. Create a PR with a clear title and concise description
6. Provide PR details as output

### Constraints
- Include only changes related to the issue
- Remove any unrelated diffs before creating the PR
- Keep commits minimal and clean
- Use Conventional Commits format for commit message and PR title
- Keep PR description concise and review-friendly
- Follow AGENTS.md
- Create PR as "Ready for review" (not Draft) unless explicitly instructed
- Confirm working tree is clean except for intended changes
- Confirm tests required by the issue have been executed
- Confirm the target branch is correct before creating the PR

### PR body format

## Summary
## Files Changed
## Tests
## Notes

### Output
- branch
- commit message
- PR title
- PR body
- PR link

---

## plan-review-response

### Goal
Review external review comments and organize a response plan WITHOUT modifying code yet.

### Workflow
1. Summarize each review comment
2. Decide whether each comment requires action: required / not required / needs confirmation
3. For comments that require action, define the minimal fix approach
4. Analyze impact and risk
5. Record unknowns and assumptions

### Constraints
- Follow AGENTS.md
- Preserve existing behavior and YAML compatibility
- Do not make unrelated changes
- Keep the future fix scope minimal
- Do NOT modify code yet

### Decision Guidance
- Mark as `not required` only when the comment is already satisfied, incorrect, or intentionally out of scope.
- Mark as `needs confirmation` when the comment changes behavior, compatibility, or scope.

### Output format

## Review Summary
## Decision (per comment)
## Plan
## Risks / Impact
## Assumptions

---

## implement-review-response

### Goal
Apply fixes based on the approved review response plan while preserving existing behavior and constraints.

### Workflow
1. Follow the approved review response plan
2. Apply only the minimal required code changes
3. Update or add tests only if necessary
4. Verify no regressions and requirements are still satisfied
5. Summarize the changes
6. List all changed files
7. Report tests executed and results
8. Record notes, assumptions, or remaining items

### Constraints
- Follow AGENTS.md
- Do not deviate from the approved plan
- Keep changes minimal
- Do not make unrelated changes
- Preserve existing behavior and YAML compatibility
- Maintain existing architecture (Controller / Service / Infrastructure)
- MUST pause if the approved review response plan is insufficient or needs to change

### Prohibitions
- No opportunistic refactoring
- No file moves or renames
- No dependency additions
- Do NOT create commits or update PRs

### Output format

## Summary
## Files Changed
## Tests
## Notes

---

## update-pr-after-review

### Goal
Reflect approved review fixes in the existing pull request.

### Workflow
1. Confirm the review fixes are complete
2. Commit only the review-related changes
3. Use a Conventional Commits commit message
4. Update the PR description if needed
5. Reply appropriately to the relevant review comments
6. Report the final PR update details

### Constraints
- Follow AGENTS.md
- Commit only the review fix changes
- Use Conventional Commits format for the commit message
- Update the PR body only if needed
- Reply appropriately to the relevant review comments

### Output format

- commit message
- updated PR description (if changed)
- list of addressed comments and responses
- PR URL

### Completion Rule
- After updating the PR, stop and wait for re-review or explicit approval.

---

## merge

### Goal
Safely merge a reviewed pull request and synchronize local/remote states.

### Preconditions
- Fetch latest remote state at start
- Fast-forward local `main` to `origin/main`
- Ensure target PR branch has no conflicts against latest `main`
- Follow AGENTS.md

### Checks
- All tests pass
- No unrelated diffs are included
- Target branch is the reviewed PR branch

### Merge Gate
- Proceed only when review is complete and explicit approval to merge has been given
- Do NOT merge while unresolved review comments remain

### Workflow
1. Fetch latest from remote
2. Fast-forward local `main` to `origin/main`
3. Verify PR branch has no conflicts with updated `main`
4. Perform squash merge
5. Push merge result to remote
6. Delete source branch (remote and local if applicable)
7. Ensure local `main` matches merged remote state
8. Close the related issue

### Output
- merge result (success / failure)
- performed checks
- final commit message
- post-merge local/remote branch status
