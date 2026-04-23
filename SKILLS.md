# SKILLS

Reusable task workflows.
Follow the steps exactly when a task name is referenced.

---

## Execution Mode

You are an executor, not a planner.

### Core Rules
- Follow steps in order (no skipping / reordering)
- Stop at defined pause points
- Do not improvise alternative workflows

### On Failure
- Stop immediately
- Report the issue
- Ask for instructions

---

## run-issue-end-to-end

### Goal
Execute full workflow from planning to merge.

### Workflow
1. plan-issue
2. implement-issue
3. review
4. create-pr

🛑 HARD STOP
Wait for review feedback

5. handle-review-loop
6. merge

---

## handle-review-loop

### Goal
Handle review until approval.

### Workflow
Repeat:
1. plan-review-response
2. implement-review-response
3. review
4. update-pr-after-review

🛑 HARD STOP
Wait for re-review

### Exit
- All comments resolved OR approval given

---

## plan-issue

### Goal
Create implementation plan (no code changes)

### Output
- Summary
- Files to Inspect
- Planned Changes
- Risks / Impact
- Tests
- Assumptions

### Rules
- No code changes
- Minimal scope
- Follow AGENTS.md

---

## implement-issue

### Goal
Implement approved plan

### Output
- Summary
- Files Changed
- Tests
- Notes

### Rules
- Follow plan strictly
- Minimal changes only
- No unrelated refactor
- Preserve behavior

---

## review

### Goal
Check code quality (no changes)

### Focus
- Readability
- Naming
- Bugs
- Test coverage
- Security

### Output
- Issues OR "No issues found"

---

## create-pr

### Goal
Create clean PR

### Must Check
- Only related changes included
- Tests executed
- Target branch correct

### Output
- branch
- commit message
- PR title
- PR body
- PR link
- PR status (must be Ready for review, not Draft)

---

## plan-review-response

### Goal
Analyze review comments

### Output
- Review Summary
- Decision (required / not required / needs confirmation)
- Plan
- Risks / Impact
- Assumptions

---

## implement-review-response

### Goal
Apply review fixes

### Rules
- Minimal changes only
- Follow plan
- No refactor

### Output
- Summary
- Files Changed
- Tests
- Notes

---

## update-pr-after-review

### Goal
Update PR after fixes

### Output
- commit message
- updated PR description (if any)
- addressed comments
- PR URL
- Must be executed only after self-review is completed

🛑 STOP after update

---

## merge

### Goal
Safely merge PR

### Preconditions
- Latest remote fetched
- Local main is up to date
- No conflicts

### Workflow
1. Fetch latest
2. Sync local main
3. Verify no conflicts
4. Squash merge
5. Push
6. Delete remote branch
7. Sync local state

### Critical (Do NOT forget)
- Close the related issue
- Ensure working tree is clean

### Completion Criteria
- Merge successful
- Issue closed (or reported if not possible)
- Local main synced with remote
- No leftover branches
- Working tree clean OR reported