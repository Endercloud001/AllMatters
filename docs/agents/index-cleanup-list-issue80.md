# #80 Index Cleanup List: Commit-Worthy vs. Separation-Needed

This document clarifies which of the currently modified/staged files should go into the #80 commit, and which should be separated for later handling or reverted.

---

## Overview

**Current State** (as of 2026-11-08):
- **5 files staged** (ready for commit to #80)
- **18 files modified** (mixed: some #80-core, some unrelated, some future-phase)
- **16 untracked files** (new documents and prototypes)

**Goal**: Clarify which files belong in the #80 commit, and which should be handled separately or reverted.

---

## Index Status Analysis

### Command to Check Current State
```powershell
git status --short
git diff --cached --name-only
git diff --stat
```

### Key Tool: Pre-Existing Patch
File: `.local/issue80-followup/pre-existing.patch` lists files modified **before** this issue started. These are baseline unrelated changes that should not enter the #80 commit.

**Pre-existing (from patch)**:
- `AGENTS.md`
- `GLOSSARY.md`
- `docs/agents/issue-tracker.md`
- `docs/v0.1.1-scope.md`
- `src/Relics/NativePlayCounters.cs`
- `src/Relics/NativeVirusPresentation.cs`
- `src/Tests/NativeCounterTests.cs`
- `tools/Check.ps1`
- `tools/ReadLocalPaths.ps1`

---

## Files to KEEP in #80 Commit (5 Staged)

These are the core #80 multiplayer editing implementation. All currently staged in index.

| File | Purpose | Change Lines | Status |
|------|---------|--------------|--------|
| `src/Jigsaw/JigsawCheckpoint.cs` | Save integration for multiplayer (checkpoint/restore) | M (staged) | ✅ Keep staged |
| `src/Jigsaw/JigsawNewRunMode.cs` | Native protocol entry point; Host mode selection | A (added, staged) | ✅ Keep staged |
| `src/Jigsaw/JigsawPreferences.cs` | Player preference tracking for multiplayer | M (staged) | ✅ Keep staged |
| `src/Tests/JigsawActivationTests.cs` | Proof that BeginRun → SetUpNewMultiplayer flow | AM (added + modified, staged) | ✅ Keep staged (test for new flow) |
| `src/Tests/JigsawNativeTests.cs` | Full integration test with ENet/protocol | MM (modified + unstaged modifications, partially staged) | ⚠️ See below |

---

## Files to SEPARATE from #80 Commit

These files have modifications related to #80 core, but ALSO have additional unstaged changes. Separate into staging order.

### JigsawNativeTests.cs
- **Staged portion** (80 lines added): Integration test for multiplayer protocol flow
- **Unstaged portion** (5 lines modified): Additional unrelated test fixtures or refactoring
- **Action**: Stage only the 80-line integration test portion; leave the 5-line refactoring unstaged for separate commit or revert

### Modified but NOT Staged (9 files)

These files have changes related to #80, but are NOT currently staged. Decide per file:

| File | Changed | Lines | Scope | Action |
|------|---------|-------|-------|--------|
| `src/Jigsaw/JigsawEditorAction.cs` | Yes | +107/-14 | Request ID, edit revision, stale rejection | ⚠️ Verify: Should this be staged for #80? |
| `src/Jigsaw/JigsawNativeSession.cs` | Yes | +153/-31 | Ancient/reward coordination | ⚠️ Verify: Should this be staged for #80? |
| `src/Jigsaw/JigsawResetCommand.cs` | Yes | +14/-9 | Reset sync and state preservation | ⚠️ Verify: Should this be staged for #80? |
| `src/Jigsaw/JigsawRun.cs` | Yes | +11/-3 | Core run state updates | ⚠️ Verify: Should this be staged for #80? |
| `src/Tests/JigsawDomainTests.cs` | Yes | +12/-1 | Domain-level test updates | ⚠️ Verify: Should this be staged for #80? |
| `src/Tests/JigsawNetworkTests.cs` | Yes | +264/-9 | Network protocol tests | ⚠️ Verify: Should this be staged for #80? |
| `src/Tests/HelloWorldTests.cs` | Minor (CRLF) | | Baseline test | ❌ Revert; no semantic change |
| `src/Relics/NativePlayCounters.cs` | Yes | TBD | Counter display for native | ❌ Not #80 scope (in pre-existing patch) |
| `src/Relics/NativeVirusPresentation.cs` | Yes | TBD | Virus presentation | ❌ Not #80 scope (in pre-existing patch) |

**Question for Clarification**: Original handoff noted that only 5 files are staged, suggesting these 9 files were NOT meant to be staged initially. Verify with user before staging them.

---

## Files to DELETE or IGNORE (Untracked Documents & Prototypes)

These are new documents created during development. Most should stay out of the #80 commit (belong in separate docs-only commit or stay untracked).

### New Documentation (Should NOT go in #80 commit)
- `docs/jigsaw-multiplayer-followup.md` — Draft explanation (move to handoff, not main repo)
- `docs/jigsaw-visual-detail-build.py` — Prototype script (separate commit if needed)
- `docs/jigsaw-visual-detail/` — Prototype directory (separate commit if needed)
- `docs/v0.1.2-scope.md` — Future scope (separate commit if needed)
- `docs/v0.1.2-battle-enhancement-in-hunting.md` — Future scope (separate commit if needed)

### New Skills/Tools (Should NOT go in #80 commit)
- `tools/CheckPrototype.ps1` — Prototype tool (separate commit if needed)
- `tools/InspectWorktree.ps1` — Prototype tool (separate commit if needed)
- `tools/ReadTextSlice.ps1` — Prototype tool (separate commit if needed)
- `tools/WriteWayfinderStatus.ps1` — Prototype tool (separate commit if needed)
- `tools/Tests/PrototypeWorkflow.Tests.ps1` — Prototype test (separate commit if needed)

### New Agent Framework Documents (Should go in separate docs commit, NOT #80)
- `docs/agents/handoff-template.md` — Framework (commit separately)
- `docs/agents/completion-standards.md` — Framework (commit separately)
- `docs/agents/issue-documentation-policy.md` — Framework (commit separately)
- `docs/agents/prototype-tools.md` — Framework (commit separately)
- `docs/agents/wayfinder.md` — Framework (commit separately)

### New Handoff Directory
- `docs/handoffs/` — Directory for future handoff documents (do not commit yet; wait for structured handoff)

---

## Pre-Existing Modifications (Should NOT be in #80 commit)

These files were already modified before #80 started, per `pre-existing.patch`. Keep them separate:

| File | Reason to Exclude | Action |
|------|-------------------|--------|
| `AGENTS.md` | Baseline documentation (pre-existing) | Revert or separate commit |
| `GLOSSARY.md` | Pre-existing glossary updates | Revert or separate commit |
| `docs/agents/issue-tracker.md` | Pre-existing documentation | Revert or separate commit |
| `docs/v0.1.1-scope.md` | Pre-existing scope doc | Revert or separate commit |
| `src/Relics/NativePlayCounters.cs` | Pre-existing counter work | Revert or separate commit |
| `src/Relics/NativeVirusPresentation.cs` | Pre-existing presentation work | Revert or separate commit |
| `src/Tests/NativeCounterTests.cs` | Pre-existing counter tests | Revert or separate commit |
| `tools/Check.ps1` | Pre-existing tool | Revert or separate commit |
| `tools/ReadLocalPaths.ps1` | Pre-existing tool | Revert or separate commit |

**Recommendation**: Create a second commit **after** #80 to address these pre-existing changes (or revert them if they were exploratory).

---

## Exact Commands to Clean Up Index

### Verify Current Staging
```powershell
git diff --cached --name-only
# Expected: Only 5 files:
# src/Jigsaw/JigsawCheckpoint.cs
# src/Jigsaw/JigsawNewRunMode.cs
# src/Jigsaw/JigsawPreferences.cs
# src/Tests/JigsawActivationTests.cs
# src/Tests/JigsawNativeTests.cs
```

### If Additional Files Are Staged, Unstage Them
```powershell
git reset HEAD src/Jigsaw/JigsawEditorAction.cs
git reset HEAD src/Jigsaw/JigsawNativeSession.cs
# ... etc for any other mistaken stages
```

### Clean Up HelloWorldTests.cs (CRLF Only)
```powershell
git checkout HEAD -- src/Tests/HelloWorldTests.cs
```

### Handle JigsawNativeTests.cs (if it has mixed changes)
```powershell
# Option 1: Stage only the green integration test portion
git add -p src/Tests/JigsawNativeTests.cs  # Interactive staging

# Option 2: View diff first
git diff src/Tests/JigsawNativeTests.cs
```

### Before Committing, Final Verification
```powershell
git diff --cached --stat
# Should show exactly: 5 files, only .cs files in src/Jigsaw/ and src/Tests/
```

---

## Commit Boundaries

### Commit #1: #80 Core Implementation (Ready Now)
```powershell
git commit -m "feat(jigsaw): support native multiplayer editing (#80)

- Implement host-client coordination for puzzle phase
- Support edit revision, request ID, and stale session rejection
- Add reset command with state preservation and broadcast
- Coordinate ancient/reward events across players
- Handle host exit, late arrival, absent players, and incomplete rewards

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

**Files included**: The 5 currently staged

### Commit #2: Pre-Existing Work (Separate, Later)
For files in `pre-existing.patch` (AGENTS.md, GLOSSARY.md, etc.):
```powershell
# After #80 is committed, stash the 5 #80 files and commit pre-existing changes separately
git stash push -- src/Jigsaw/ src/Tests/Jigsaw* docs/handoffs  # Preserve #80 work
git commit -m "docs: update agent tracking, glossary, scope references"
git stash pop  # Restore #80 work if needed
```

### Commit #3: New Documentation (Later, If Approved)
For new framework docs and tools:
```powershell
# Commit new agent framework documents separately
git add docs/agents/handoff-template.md docs/agents/completion-standards.md docs/agents/issue-documentation-policy.md
git commit -m "docs(agents): add implementation framework and standards

- Add handoff template for clear Agent context transfer
- Define completion three-phase standards
- Clarify issue documentation responsibility boundaries"
```

---

## Decision Matrix

**Q: Should file X go into #80 commit?**

| Condition | Answer | Action |
|-----------|--------|--------|
| File is in pre-existing.patch | ❌ NO | Separate commit or revert |
| File is new documentation (not code) | ❌ NO | Separate docs commit or untracked |
| File is new tool/prototype | ❌ NO | Separate commit if needed, or keep untracked |
| File is in `src/Jigsaw/Jigsaw*.cs` and has +/- | ✅ YES | If unstaged, verify before staging |
| File is in `src/Tests/Jigsaw*Tests.cs` and has +/- | ✅ YES | If unstaged, verify before staging |
| File is `src/Tests/HelloWorldTests.cs` with only CRLF | ❌ NO | Revert (no semantic change) |
| File is in `src/Relics/` | ❌ NO | Pre-existing, separate commit |
| File is checkpoint/run state related to multiplayer | ✅ YES | Stage for #80 |

---

## Summary: Index Status After Cleanup

**To Achieve Clean Staging:**
1. Verify 5 files are staged: ✅
2. Revert HelloWorldTests.cs (CRLF only): ✅
3. Unstage any accidentally staged pre-existing files (AGENTS.md, GLOSSARY.md, etc.): Verify and unstage
4. Unstage any accidentally staged files not in `src/Jigsaw/` and `src/Tests/Jigsaw*`: Verify and unstage
5. Separate JigsawNativeTests.cs if it has mixed changes: Use `git add -p` to stage only integration test portion
6. Verify final `git diff --cached --stat` shows only the 5 intended files: ✅

**After cleanup, index is ready for Phase 2 code review and eventual commit.**

---

## References

- [Completion Standards](../agents/completion-standards.md) — Phase 1 (dev complete) verifies staged changes
- [Handoff Template](../agents/handoff-template.md) — Not in Scope section clarifies boundaries
- `.local/issue80-followup/pre-existing.patch` — Baseline modifications to exclude
