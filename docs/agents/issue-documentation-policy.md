# Issue Documentation Policy: Clear Responsibility Boundaries

This policy clarifies where information lives and who is responsible for maintaining it. The goal is to eliminate duplication, ensure specs stay current, and prevent stale handoffs.

---

## Core Principle: Single Source of Truth

**Each type of information lives in one place.** When deciding where to put something, ask: "Is this a spec, a handoff note, or implementation detail?" Then follow the policy below.

---

## 1. Specification Authority (Specs Live in GitHub Issues & ADRs)

### What Goes Here
- **Issue requirements**: Feature description, acceptance criteria, boundary conditions
- **Architecture decisions**: Trade-offs, alternatives considered, decisions made
- **Design constraints**: Performance requirements, compatibility, known limitations

### Format
- **Primary issue**: GitHub issue body; updated only when user or team updates the requirement
- **ADRs**: `docs/adr/<number>-<title>.md` for significant architecture decisions (see ADR template in repo)
- **Boundary clarifications**: `docs/jigsaw-*.md`, `docs/v0.1.*.md` for feature-specific scope boundaries

### Who Maintains
- **User**: Changes requirements (via GitHub issue edit or comment)
- **Agents**: Read specs; do NOT copy specs into handoff documents
- **Rule**: If spec changes, update GitHub issue or ADR. Do not update the handoff retroactively.

### Example
❌ **Bad**: Handoff document repeats the full spec for #80 (134 lines of duplication)
✅ **Good**: Handoff links to GitHub issue #80 and references only the relevant decision (e.g., comment #12345)

---

## 2. Handoff Notes (Transient, Per-Issue, Per-Session)

### What Goes Here
- **Current progress**: What's done, what's pending, what's blocked
- **Evidence locations**: Where to find test logs, validation results
- **Next agent's immediate tasks**: Unambiguous action items in priority order
- **Process steps**: Exact git commands, test commands, verification steps to execute
- **Known pitfalls**: Runtime traps, common mistakes, things to avoid

### Format
- **Primary location**: `docs/handoffs/AllMatters-issue<N>-handoff-YYYY-MM-DD.md` (timestamped)
- **Structure**: Use the [Handoff Template](handoff-template.md)
- **One per issue**: Each issue has at most one active handoff; prior handoffs become read-only history

### Lifecycle
1. **Created**: When work pauses or transfers to a new agent
2. **Updated**: Only if work resumes and prior handoff is still accurate
3. **Archived**: When issue closes; moved to `.local/issue<N>/handoffs-archive/` or deleted if superseded
4. **Never pushed to main**: Handoffs are working documents, not repository history

### Who Maintains
- **Implementing agent**: Creates/updates on session end (if work continues)
- **Incoming agent**: Reads handoff at start; confirms it matches `git status` and prior logs
- **Retiring agent**: Cleans up stale handoffs when issue closes

### Example
❌ **Bad**: Handoff repeats spec from GitHub #80 + copies all test commands + lists architecture decisions
✅ **Good**: Handoff links to GitHub #80, lists only what's new (e.g., "Phase 1 evidence at `.local/issue80-followup/*-green.log`"), and references exact test commands with their output paths

---

## 3. Implementation Details (Live in Code Comments & Commit Messages)

### What Goes Here
- **Why a design choice**: Code comment explaining non-obvious logic
- **Workarounds**: Why we're doing something unusual or inefficient (with issue reference)
- **Seams and boundaries**: Where code interfaces with native API, game engine, or external systems
- **Assumptions**: What must be true for code to work (e.g., "Ritsu 0.6.7 API contract")

### Format
- **In-code comments**: Near the code being explained; reference the issue or ADR
- **Commit messages**: Explain *why* the change, not what; reference the issue
- **NOT a separate docs/ file**: Don't create new explanation documents; explain in code

### Example in Code
```csharp
// #80: Host must call RunManager.Launch() to set LocalContext identity
// and release network message buffer. Prior versions had this in test listeners
// and caused Host-only execution (client never received message).
// See: JigsawNetworkTests.cs for actual protocol flow.
RunManager.Launch();
```

### Example in Commit
```
feat(jigsaw): implement host-client reset synchronization (#80)

Maintain original pool, reset all temporary state, preserve order of acceptance.
Do not report async results as complete until they actually apply.

Fixes #80. Related to #76 (save checkpoints).
```

### Who Maintains
- **Implementing agent**: Writes as code is written
- **Code reviewers**: Check comments are accurate when reviewing diffs
- **Retiring agent**: Update comments if assumptions change, reference new issues

---

## 4. Decision Record (ADRs for Significant Choices)

### When to Create an ADR
- Feature interacts with multiple subsystems (e.g., native → multiplayer → save)
- Trade-offs are material (e.g., speed vs. simplicity, compatibility vs. correctness)
- Decision might be revisited later (e.g., "Why is reset synchronous in Phase 1?")
- Multiple agents may work on related code (e.g., #80 and #81)

### When NOT to Create an ADR
- Simple implementation choice (use code comment instead)
- Decision is obviously correct (e.g., "Always validate user input")
- No trade-offs to document (e.g., "Bug fix, only one right solution")

### Format
- **Location**: `docs/adr/<number>-<title>.md` (e.g., `docs/adr/0005-host-authority-for-reset.md`)
- **Template**: Use repo's ADR template or follow [MADR format](https://github.com/adr/madr)
- **Contents**:
  - Problem statement
  - Considered options
  - Decision and reasoning
  - Consequences (positive & negative)
  - References (GitHub issues, related ADRs)

### Lifecycle
- **Created**: During implementation when trade-off is discovered
- **Finalized**: Before PR review; linked in commit message or issue comment
- **Obsoleted**: When decision is reversed; new ADR references the old one

### Example
- ✅ `docs/adr/0003-multiplayer-protocol-versioning.md`: Why request IDs use sequence numbers, not hashes
- ✅ `docs/adr/0004-save-manager-isolation.md`: Why MockGodotFileIo is needed for testing
- ❌ "Revert all player modifications to starting pool" (this is implementation, not a decision)

---

## 5. Test Evidence (Logs & Artifacts Live in `.local/`)

### What Goes Here
- **Test execution logs**: `.local/issue<N>/evidence/` or `.local/issue<N>-followup/`
- **Build artifacts**: Package outputs, binaries
- **Protocol traces**: Network/message capture for debugging
- **Setup scripts**: Reproducers for specific scenarios

### Format
- **Naming**: `<scenario>-<result>.log` (e.g., `integration-final-green.log`, `reset-network-red.log`)
- **Location**: Organized by issue number and concern (evidence, logs, scenarios)
- **Retention**: Kept at least until issue closes; archive or delete after

### Who Maintains
- **Implementing agent**: Generates during development; keeps green logs as proof
- **Reviewing agent**: Checks logs exist and match expectations; may re-run to verify
- **Retiring agent**: Archives or deletes when issue closes

### Example
✅ Good organization:
```
.local/
├── issue80-followup/
│   ├── evidence/
│   │   ├── integrated-final-green.log
│   │   ├── ancient-whole-checkpoint-green.log
│   │   └── pending-reward-handoff.txt
│   ├── logs/
│   │   ├── protocol-*.log
│   │   ├── reset-network-*.log
│   │   └── hostexit-*.log
│   └── references/
│       └── ancient-api.md
```

---

## 6. Transition Rules: When Information Moves

### Case: Issue Closes
1. **Spec** (GitHub issue): Close issue; spec is now historical; new issues reference the closed issue
2. **Handoff**: Move to archive or delete; no longer needed
3. **ADRs**: Remain in repo; reference the closed issue
4. **Code comments**: Remain in code; keep explaining the "why"
5. **Test logs**: Archive to `.local/issue<N>/archive/` or delete if space needed

### Case: Issue Branches (e.g., #80 → #80a and #80b)
1. **Spec** (GitHub issue): Create new issues for sub-tasks; link to parent via GitHub sub-issues feature
2. **Handoff**: Create new handoff for each sub-task
3. **ADRs**: Reference both old and new issues
4. **Code comments**: Update references to clarify which sub-task they belong to
5. **Test logs**: Organize by sub-task ID

### Case: Issue Blocked on Another Issue
1. **Spec**: Link explicitly in GitHub (issue dependencies feature)
2. **Handoff**: Document the blocker (what, why, who must resolve)
3. **ADRs**: Reference blocking issue
4. **Code**: Comment references both issues
5. **Logs**: Keep separate; don't merge evidence until blocker clears

### Case: Agent Must Read Prior Implementation
1. **Start with**: GitHub issue (understand requirement)
2. **Then read**: ADRs (understand decisions & trade-offs)
3. **Then read**: Code comments (understand implementation why)
4. **Then read**: Handoff (understand current state & evidence)
5. **Then read**: Test logs (understand what's been validated)
6. **Then check**: Running `git status` and `git diff` (current state)

---

## 7. Policy Violations & Cleanup

### Common Violations
❌ **Specs duplicated in handoff**: Handoff repeats full GitHub issue text
❌ **Handoffs in code**: Implementation details written as separate `.md` files
❌ **ADRs for trivial choices**: "Why did we use `const` instead of `let`?" (use code comment)
❌ **Test logs committed**: `*.log` files in repo instead of `.local/`
❌ **Outdated handoffs**: Old handoff says "Phase 1 pending" but code is already Phase 3

### Fixing Violations
1. **Move specs to GitHub issue**: If spec is in handoff, move to issue body/comment and link
2. **Move implementation to code comments**: If explanation is in separate doc, move into code
3. **Create ADR**: If architectural decision is scattered, consolidate into single ADR
4. **Delete handoff**: When issue closes, handoff is no longer needed
5. **Update handoff**: When resuming work, verify handoff still matches `git status`

### Detection
- **Spec duplication**: Handoff length > 50 lines (rule of thumb: should be ~30 lines)
- **Implementation in docs**: Files in `docs/jigsaw-*/` explaining code logic
- **Outdated handoff**: Handoff conflicts with current `git status` or test logs

---

## Summary Table

| Type | Lives In | Who Writes | When Finalized | Deleted When |
|------|----------|-----------|-----------------|--------------|
| **Spec** | GitHub issue + ADR | User + core team | Issue created | Never (historical record) |
| **Handoff** | `docs/handoffs/` | Implementing agent | Work pauses | Issue closes |
| **Implementation** | Code comments | Implementing agent | Code reviewed | Never (historical record) |
| **Decision** | `docs/adr/` | Implementing agent (with review) | Before PR merge | Never (reference old in new ADR) |
| **Test Evidence** | `.local/issue*/` | Test run / implementing agent | Test complete | Manually archived/deleted |

---

## For Incoming Agents: Reading Order

When you take over an issue:

1. **Read spec** (GitHub issue end-to-end)
2. **Read ADRs** (if any, in `docs/adr/` or linked in issue)
3. **Read handoff** (current issue handoff, if exists)
4. **Run verification** (`git status`, test commands, check logs exist)
5. **Read code** (understand implementation so far)
6. **Proceed** (next agent task from handoff)

**If any of these are missing or contradictory, stop and document the gap before proceeding.**

---

## Updating This Policy

This policy evolves as the project grows. To suggest changes:

1. Document your observation (e.g., "X type of info has nowhere to go")
2. Propose a fix (which type of doc, new location, lifecycle)
3. Raise as GitHub discussion or mention in handoff
4. Policy updates require team consent; document decisions in `docs/agents/` updates

Current version: 2026-11-08 (established with Phase 2 completion standards framework)
