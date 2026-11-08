# Agent Implementation Handoff Template

This template ensures that incoming agents have unambiguous context to continue work on an issue. Use this structure for all Agent handoff documentation to eliminate interpretation ambiguity and reduce onboarding friction.

---

## TL;DR

**Priority**: [Critical / High / Medium / Low]

**Current Status**: [Brief one-line state, e.g., "Core implementation complete; awaiting final code review and package verification"]

**Blocker** (if any): [What is blocked and why, or "None"]

**Next Agent Should**: [One imperative statement, e.g., "Complete the final two-axis code review (Standards & Spec), then run verification checks and commit"]

---

## Spec Authority

**Issue**: [Link to GitHub issue, e.g., `#80`]

**Parent Spec** (if applicable): [Link to parent issue or spec document, e.g., `#73`, `docs/v0.1.1-scope.md`]

**Specifications** (do not duplicate; link only):
- [Linked document or issue describing what must be implemented]
- [Any architecture decision records (ADRs) in `docs/adr/`]
- [Boundary clarifications in `docs/jigsaw-*/` or related]

**User-Confirmed Decisions**: [Link to specific GitHub comments or decisions already made, e.g., comment URL from issue where user confirmed responsibility boundaries]

---

## Remaining Work

List all outstanding tasks in priority order. Each task must include:
1. **Action**: What to do (imperative)
2. **Verification**: How to confirm it's done (test command, log path, or manual check)
3. **Acceptance**: Condition for moving to next phase

Example format:

### Task 1: [Title]
- **Action**: [What needs to be done]
- **Verification**: Run `<command>` or check `<path>` for result; expect `<output>`
- **Acceptance**: [Criterion for done, e.g., "All green logs in `.local/issue80-followup/integrated-final-green.log`"]

### Task 2: [Title]
- **Action**: [...]
- **Verification**: [...]
- **Acceptance**: [...]

---

## Evidence Locations

Provide paths to all proof that prior work is valid:

**Test Logs**: Path format `.local/<issue-number>-followup/` (read-only reference)
- `integration-flow-*.log`: Full flow integration evidence
- `protocol-*.log`: Protocol/contract validation
- `reset-*.log`: State reset behavior
- `ancient-*.log`: Ancient/reward handling
- `pending-*.log`: Late-arrival and pending-completion handling

**Implementation Notes**: 
- `.local/<issue-number>-followup/handoff-*.txt`: Detailed findings on specific subsystems
- `.local/<issue-number>-followup/ancient-api.md`: Native API details (if applicable)

**Reference Materials**:
- `.local/references/`: Read-only copies of upstream source for comparison
- `docs/game-mechanics.md`: Call chain documentation for feature areas

**Build & Test Artifacts**:
- Where unit tests live: `src/Tests/` path
- How to run them: Standard command from `tools/Test.ps1`
- Package output: `bin/Release/` after build

---

## Process

Follow these steps in order. Each step must complete verification before moving to the next.

### Phase 1: Dev Complete
Validate that code is ready for testing.

1. **[Step Name]**
   - Read: [Files to understand]
   - Check: `git status` shows 5 staged, N modified; compare against `pre-existing.patch` for unrelated changes
   - Verify: [Expected state, e.g., "No unrelated files in index"]

2. **[Step Name]**
   - Action: [Command to run]
   - Expected: [Success output or log state]

### Phase 2: Review Ready
Prepare staged changes for formal two-axis code review.

1. **Isolate changes**: Ensure `git diff --cached <commit-hash> -- <paths>` shows only this issue's changes
2. **Run standards review**: Use `code-review` skill with fixed starting commit and explicit path list
3. **Run spec review**: Separate agent reads same diff against spec authority

### Phase 3: Ship Ready
Final verification before commit.

1. **Pass all checks**: Run full test suite; confirm no regressions
2. **Build package**: Execute build; confirm no errors
3. **Commit**: Stage clean index; provide commit message with co-author attribution

---

## Not in Scope

Explicitly list what is **not** covered by this handoff and belongs to separate issues or future phases:

- **What**: [e.g., "User machine testing with game client or Steam"]
- **Why**: [e.g., "Manual acceptance tied to separate issue #81"]
- **Owned By**: [e.g., "User's manual testing, not automated verification"]

- **What**: [e.g., "Network synchronization for multi-player exploration lobbies"]
- **Why**: [e.g., "Belongs to issue #81 per dependency chain"]
- **Owned By**: [e.g., "Future implementation in separate session"]

---

## Historical Context

**Session(s) that worked on this**: [List session names/IDs, e.g., "`allmatters-implementation-80-multiplayer`, `allmatters-followup-80-cleanup`"]

**Prior Handoff(s)**: [Link to predecessor handoff documents if any, or "None — this is initial handoff"]

**Why Progress Restarted**: [Brief explanation if work was interrupted, e.g., "Mid-turn interruption for unrelated user request; all prior changes preserved"]

---

## Suggested Skills

For the incoming agent's reference (not mandatory):

- **`implement` + `tdd`**: Continue with test-driven development; new tests required for uncovered cases
- **`code-review`**: Execute formal two-axis review; use fixed starting commit from spec authority
- **`diagnosing-bugs`**: If new runtime failures appear; reproduce issue first, then debug
- **`verification-before-completion`**: Before claiming work done; use fresh verification output, not prior logs

---

## Quick Sanity Checks

Before starting, confirm:
- [ ] You've read the GitHub issue (spec authority) end-to-end
- [ ] You can access all Evidence Locations (log paths, test command)
- [ ] `git status` matches the "Remaining Work" expectations (staged vs modified counts)
- [ ] No prior build processes are still running (check task manager / `ps`)
- [ ] You understand the Not in Scope boundaries and won't exceed them

---

## Questions for Clarification

**If any of these are unclear, ask before proceeding:**
- Which test command(s) should I run first?
- What does "Phase 2" actually mean in terms of concrete git commands?
- Are there any environment variables or setup steps I need to know?
