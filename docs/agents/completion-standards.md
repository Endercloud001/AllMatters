# Agent Implementation: Completion Standards (Three-Phase Framework)

This document defines when work is complete at each phase. Use this framework to eliminate ambiguity in handoffs and to know exactly what "done" means before submitting a PR or closing an issue.

---

## Overview: Three Phases of Completion

All implementation work passes through three phases:

1. **Phase 1: Dev Complete** — Code is testable and basic scenarios pass
2. **Phase 2: Review Ready** — Changes are staged, isolated, and ready for formal review
3. **Phase 3: Ship Ready** — All checks pass; code is committed and ready to merge

Each phase has specific, checkable criteria. Do not claim a phase is complete until all criteria are met.

---

## Phase 1: Dev Complete

**Goal**: Code compiles, basic test scenarios execute, and changes are staged for review.

### Criteria

1. **Code compiles without errors**
   - Verify: Run build command (e.g., `dotnet build` or project build tool)
   - Success: No compilation errors; warnings are OK if pre-existing
   - Failure mode: Compilation errors block all further work

2. **Specific scenarios pass as proved by evidence**
   - Verify: Run test command(s) specified in the issue
   - Success: Test logs show green or "PASSED" for target scenarios
   - Evidence location: `.local/<issue-number>/evidence/` or `.local/<issue-number>-followup/` logs
   - Acceptance: All logs named `*-green.log` exist; failure logs should be corrected

3. **Staged changes are clear and isolated**
   - Verify: `git status` shows files staged in index
   - Success: Only files belonging to this issue are staged (use pre-existing snapshot to identify unrelated changes)
   - Failure mode: Unrelated files mixed in — must unstage and preserve for later
   - Command to check: `git diff --cached --name-only | sort` should only list issue-specific paths

4. **No unrelated modifications in the working tree**
   - Verify: Compare `git status` modified files against `pre-existing.patch` from issue start
   - Success: Unrelated files are either not modified, or modifications are clearly separate (noted for later)
   - Failure mode: Accidentally editing files not in scope — save them separately or revert

### Transition Criteria to Phase 2
- All criteria above are met
- Handoff document is prepared with Evidence Locations and Next Steps
- Prior agent has verified no stale processes or interrupted builds

---

## Phase 2: Review Ready

**Goal**: Staged changes are formally reviewable against spec, with clear boundaries and no ambiguous diffs.

### Criteria

1. **Staged diff has a fixed, documented starting point**
   - Verify: Handoff specifies exact commit SHA or tag (e.g., `a43f61e` or `v0.1.1-release`)
   - Success: `git diff --cached <starting-commit> -- <paths>` produces consistent, reproducible diff
   - Failure mode: No fixed point → diff is ambiguous if newer commits land

2. **Staged changes cover only the stated issue scope**
   - Verify: `git diff --cached <starting-commit> -- <paths>` shows only files/code blocks in scope
   - Success: No files outside the issue's listed paths appear in diff
   - Compare against: Issue specification and `handoff-template.md` Not in Scope section
   - Failure mode: Scope creep detected — either remove changes or document as separate follow-up

3. **Both reviewers use identical diff and spec**
   - Verify: Both reviewers receive same `git diff` command with explicit paths
   - Success: Reviewers read the exact same changes
   - Reviewers have: Spec authority (GitHub issue/ADR), code context (read-only access to full repo), and prior logs (read-only)
   - Failure mode: Different reviewers see different diffs → re-generate and re-review

4. **Review process is structured (two axes)**
   - Standards Axis: Does code follow repo coding standards, naming, patterns?
   - Spec Axis: Does code match the spec authority (issue, ADR, boundary doc)?
   - Each axis reviewed independently; findings reported separately
   - Failure mode: Ad-hoc feedback with unclear basis → use structured review instead

### Transition Criteria to Phase 3
- Standards review complete with findings resolved
- Spec review complete with findings resolved
- All modified code re-staged after fixes
- Tests re-run and re-confirmed; logs updated
- Ready to commit

---

## Phase 3: Ship Ready

**Goal**: All automated and manual checks pass; code is committed and eligible for merge.

### Criteria

1. **All applicable automated checks pass**
   - Verify: Run full test suite (e.g., `./tools/Test.ps1 -Branch All`)
   - Success: All tests in scope for this issue pass (green logs in `.local/` or test output)
   - Failure mode: Test failures → fix in Phase 1/2, re-review, then re-run
   - Note: Do NOT reuse old 301-item passing logs from prior sessions; use fresh run for current work tree

2. **Package builds successfully**
   - Verify: Run package build command (e.g., `dotnet publish`)
   - Success: No compilation errors; package artifacts appear in expected directory
   - Failure mode: Build errors → fix in implementation, re-stage, re-review
   - Output location: Documented in issue handoff or development guide

3. **No regressions in prior passing scenarios**
   - Verify: Re-run regression test suite (if separate from primary tests)
   - Success: All prior tests still pass; no new failures
   - Failure mode: Regression detected → fix in Phase 1/2, re-review

4. **Commit message is clear and properly attributed**
   - Format: `feat(<area>): <description> (#<issue-number>)` followed by blank line, optional body, and co-author line
   - Example: 
     ```
     feat(jigsaw): support native multiplayer editing (#80)
     
     - Implement protocol for host-client coordination
     - Add reset command synchronization
     
     Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
     ```
   - Verify: Message matches project conventions (check prior commits or CONTRIBUTING.md)

5. **Index state is clean and ready to commit**
   - Verify: `git diff --cached` shows only the final, reviewed code
   - Success: No stray files, no unfinished work
   - Command: `git commit -m "<message>"` will commit exactly what's staged

### Transition Criteria to Merge
- All criteria above are met
- User approves commit (no auto-push without explicit approval)
- If PR exists: Merge conflicts resolved, checks pass, ready to click "Merge"
- If direct commit: Pushed to main (if repo policy allows), or ready for manual push review

---

## State Transitions: What Blocks Progress

### Blocking Phase 1 → Phase 2
- Compilation errors (fix in code, rebuild, re-test)
- Test failures (fix in code, re-run, confirm green logs)
- Unrelated files staged or modified (unstage, separate, or revert)
- No evidence of passing scenarios (run tests, capture logs)

### Blocking Phase 2 → Phase 3
- Standards review feedback not addressed (implement fixes, re-stage, re-review)
- Spec review feedback not addressed (implement fixes, re-stage, re-review)
- Scope creep detected (document as separate follow-up issue, remove from current PR)
- Different reviewers seeing different diffs (standardize git command, re-review)

### Blocking Phase 3 → Ship
- Automated tests fail (fix, re-run, confirm)
- Package build fails (fix, rebuild)
- Regressions in prior scenarios (investigate, fix, re-test)
- Commit message doesn't match conventions (reword before pushing)

---

## Checkpoints & Verification

### Before Claiming Phase 1 Complete
- [ ] `dotnet build` succeeds (or equivalent for your language)
- [ ] Test logs exist in `.local/<issue>/evidence/` or `.local/<issue>-followup/`
- [ ] Logs show green for all target scenarios
- [ ] `git status` shows staged files matching issue scope
- [ ] `git diff --cached <starting-commit> -- <paths>` shows only in-scope changes

### Before Claiming Phase 2 Complete
- [ ] Both reviewers used identical `git diff --cached` command
- [ ] Both reviewed the same spec authority (issue/ADR/boundary doc)
- [ ] Standards feedback addressed and re-staged
- [ ] Spec feedback addressed and re-staged
- [ ] Tests re-run after fixes; logs updated
- [ ] No files outside scope in staged diff

### Before Claiming Phase 3 Complete
- [ ] Full test suite passes (fresh run, not prior 301-item logs)
- [ ] Package builds with no errors
- [ ] No regressions detected
- [ ] Commit message follows conventions
- [ ] `git diff --cached` is empty (all changes committed or discarded)

---

## Special Cases

### When Scope Changes
- Document the change in handoff / GitHub issue
- Identify which changes belong to **new** scope vs. **original** scope
- Move new-scope changes to separate branch or issue
- Re-review only original scope
- Ship original scope; start separate PR for new scope

### When Review Feedback Requires New Tests
- Implement tests in Phase 1
- Re-run all tests to confirm green
- Include new tests in next Phase 2 review
- Do not claim Phase 2 complete without re-review after adding tests

### When Regression Is Found Late
- Do NOT claim Phase 3 complete
- Return to Phase 1 (fix implementation)
- Re-run all tests
- Re-stage and re-review (Phase 2)
- Re-run full suite and re-verify (Phase 3)

### When an Issue Is Blocked
- Clearly document the blocker in handoff
- Identify which agent/issue/user action is required
- Do NOT proceed to later phases while blocked
- Update handoff when blocker is cleared

---

## Example: Issue #80 Progress Tracking

**Current State** (as of 2026-11-08):
- **Phase 1**: ✅ Complete
  - Core implementation done
  - Evidence exists in `.local/issue80-followup/*-green.log`
  - 5 files staged (JigsawCheckpoint, JigsawNewRunMode, JigsawPreferences, 2 test files)

- **Phase 2**: ⏳ Not yet started
  - Needs two-axis code review (Standards & Spec)
  - Starting commit: `a43f61e6f66b0935ce3dac5b7088d184f338e535`
  - Scope paths: `src/Jigsaw/Jigsaw*.cs` and `src/Tests/Jigsaw*Tests.cs`

- **Phase 3**: ⏳ Blocked until Phase 2 complete
  - Full check suite must be run
  - Package built
  - Commit prepared

---

## Communication Template

Use this when handing off between agents or updating the issue:

```
**Current Phase**: Phase X (Dev/Review/Ship Complete)

**Criteria Met**:
- [List what's done]
- [Evidence locations]

**Criteria Pending**:
- [List what's needed to progress]

**Blocker** (if any): [What's preventing Phase X+1]

**Next Agent Should**: [One clear action]
```

---

## References

- [Handoff Template](handoff-template.md): Structure for issue handoffs
- [Issue Documentation Policy](issue-documentation-policy.md): Where specs live vs. handoff vs. code
- [Code Review Guide](review.md): How to execute standards & spec reviews
- `.local/<issue>/evidence/`: Where test logs are stored
