# Post-Issue Template Improvements: Recommendations for #81 and Future Issues

This document suggests improvements to how future GitHub issues are written, based on what we learned from #80 and the new completion standards framework.

---

## Lessons from #80

**What Worked Well:**
- Clear boundary doc (`jigsaw-multiplayer-boundary.md`) explaining scope limits
- Specific acceptance criteria (core scenarios: protocol, reset, ancient, etc.)
- User confirmation of responsibility boundaries (GitHub comment reference)

**What Could Be Improved:**
- Issue description conflated multiple info layers (spec + implementation notes)
- No explicit "Phase 1 / Phase 2 / Phase 3" language in acceptance
- Handoff documentation wasn't structured until midway through
- Scope ambiguity between #80, #76, #81 took multiple clarifications

---

## Recommended Issue Template for Implementation Issues

### For New Implementation Issues (#81 and Beyond)

Use this template when creating a GitHub issue for implementation work:

```markdown
# Feature: [Title] (#N)

## Summary
[One paragraph: what feature, why it matters, context]

## Specification Authority
- **Parent Issue**: #X
- **Related Specs**: 
  - docs/adr/NNNN-title.md (if exists)
  - docs/jigsaw-*.md (boundary clarifications)
- **User-Confirmed Decision**: [Link to comment or decision]

## Acceptance Criteria (Phase 1: Dev Complete)

Code must compile, unit tests pass, and staged changes are isolated.

- [ ] Core implementation added to `src/Jigsaw/Jigsaw*.cs`
- [ ] Unit tests added to `src/Tests/Jigsaw*Tests.cs`
- [ ] All target scenarios pass (see Evidence section)
- [ ] Staged changes limited to issue scope (see Boundaries section)

### Target Scenarios
- [Scenario 1]: [Brief description]
- [Scenario 2]: [Brief description]
- [Scenario 3]: [Brief description]

## Acceptance Criteria (Phase 2: Review Ready)

Staged code passes both Standards and Spec axis reviews.

- [ ] Standards review complete (naming, patterns, style)
- [ ] Spec review complete (matches acceptance criteria)
- [ ] All must-fix findings addressed
- [ ] Tests re-run after fixes; green logs updated

## Acceptance Criteria (Phase 3: Ship Ready)

All checks pass, package builds, code is committed.

- [ ] Full test suite passes (fresh run, not prior logs)
- [ ] Package builds with no errors
- [ ] No regressions in prior scenarios
- [ ] Commit message follows project conventions
- [ ] Ready to merge (no auto-push without approval)

## Boundaries

### In Scope (This Issue)
- [Feature area 1]
- [Feature area 2]
- [Specific test scenarios]

### Out of Scope (Future Issues)
- [Feature area A] → Issue #X (reason)
- [Feature area B] → Issue #Y (reason)
- [Manual user testing] → User's acceptance process

## Implementation Notes

**Call Chain Entry Points:**
- [Function/class where feature starts]
- [Integration point with native/engine]

**Known Constraints:**
- [Runtime limitation or API contract]
- [Compatibility issue or workaround]

**Related Code:**
- [Prior implementation or related feature in src/]

## Evidence Locations (After Implementation)

Place test logs in `.local/issue<N>-followup/`:
- `integration-final-green.log` — Full flow proof
- `<scenario>-{green,red}.log` — Per-scenario evidence
- Handoff document with Phase 1/2/3 progress

## References

- Issue #X (parent/dependency)
- docs/game-mechanics.md#section (call chain reference)
- [Link to archived prior investigation if any]

---

## Acceptance Criteria Checklist

**Before closing this issue:**
- [ ] User confirms all Phases (1/2/3) complete
- [ ] No Blocker or Blocked By issues remain
- [ ] Code merged to main
- [ ] Handoff document archived or cleaned up
- [ ] Pre-existing modifications separated into new issue (if any)

---
```

### Key Improvements Over #80

1. **Explicit Phases**: Acceptance criteria broken into Phase 1, 2, 3 language (maps to completion standards)
2. **Clear Scenarios**: Target scenarios listed upfront (easier to verify in logs)
3. **Boundaries Section**: Out of Scope clearly linked to dependent issues (prevents scope creep)
4. **Evidence Locations**: Tells implementer where to put logs BEFORE starting (structure preserved)
5. **Implementation Notes**: Upfront constraints and API details (avoid runtime pitfalls)
6. **Checklist Before Closing**: Confirms all phases done and code merged (not just implementation complete)

---

## For Issue #81 Specifically

Based on #80 learnings, here's how to structure #81 (Exploration & Lobby Recovery):

**Suggested Approach:**
1. Clarify whether #81 is one issue or should be split into:
   - 81a: Lobby state synchronization (depends #76, #80)
   - 81b: Exploration UI recovery (depends #80)
   - 81c: Save restoration & fallback
   - 81d: Integration & full-flow testing

2. Use new template for each sub-issue
3. Link dependencies explicitly in GitHub (sub-issues feature)
4. Clarify which manual testing is out of scope (belongs in user's acceptance process, not automation)

**Example for 81a:**
```markdown
# Jigsaw: Lobby State Synchronization (#81a)

## Summary
Synchronize lobby state across players when returning from Ancient rewards, 
before entering next exploration session.

## Spec Authority
- Parent: #81 (Epic: Exploration & Lobby Recovery)
- Related: #80 (multiplayer editing), #76 (save checkpoints)

## Acceptance Criteria (Phase 1)
- [ ] Host broadcasts lobby state after Ancient phase
- [ ] Clients receive and apply state update
- [ ] Late-arriving players get state on connection
- [ ] Test: MultiplayerLobbySync.cs scenarios all green

## Out of Scope
- Actual exploration UI rendering (belongs to 81b)
- Player reconnection retry logic (belongs to network layer)
- User manual testing (belongs to acceptance)

## Evidence Locations
- `.local/issue81a-followup/lobby-sync-*.log`
- `src/Tests/MultiplayerLobbySync.cs`

...
```

---

## Communication Template for Handoff

When handing off between agents, include:

```markdown
**Issue #XX Handoff Summary**

**Phase Status**: Phase X of 3 (Dev / Review / Ship)

**What's Done**:
- [Specific implementation]
- Evidence: `.local/issue<N>/evidence/*-green.log`

**What's Next**:
1. [Action 1] — Verify with [command]
2. [Action 2] — Check [log path]

**Blocker** (if any): [What, why, who resolves]

**Read Before Starting**:
- GitHub issue #XX (spec)
- Handoff: `docs/handoffs/AllMatters-issue<N>-handoff-YYYY-MM-DD.md`
- Previous handoff (if any): [link]

**Suggested Skills**: [implement], [tdd], [code-review]
```

---

## Process Improvements Going Forward

### 1. Handoff Timing
- **When**: At end of each session, if work continues next session
- **Where**: `docs/handoffs/AllMatters-issue<N>-handoff-YYYY-MM-DD.md`
- **Format**: Use [handoff-template.md](handoff-template.md)

### 2. Phase Markers
- **Phase 1 Done**: Create tag or comment "Phase 1 complete" with evidence log names
- **Phase 2 Done**: Create GitHub check or comment "Phase 2 complete (Standards & Spec reviews done)"
- **Phase 3 Done**: Commit message contains all three phases verified; ready to merge

### 3. Pre-Commit Cleanup
- **Before Phase 2**: Run `git diff --cached --stat` to verify exact file list
- **Before Commit**: Double-check against `pre-existing.patch` to avoid mixing unrelated changes
- **After Phase 2 Fixes**: Re-stage only the fixed files; don't add extras

### 4. Documentation Living in Code
- **Not in separate `.md` files**: Implementation details and workarounds live in code comments
- **With issue references**: `// #80: Reason for unusual pattern` in code
- **Reviewed during code review**: Comments checked for accuracy alongside logic

---

## Quick Reference: What Goes Where

| Information Type | Location | Who Updates | When |
|---|---|---|---|
| **Feature requirement** | GitHub issue | User | Before implementation |
| **Architecture decision** | `docs/adr/` | Implementer | During implementation |
| **Boundary clarification** | `docs/jigsaw-*.md` | Core team | Before implementation |
| **Current progress** | Handoff `.md` | Implementing agent | Session end (if continuing) |
| **Why we did it this way** | Code comment | Implementer | As code is written |
| **Test proof** | `.local/issue*/evidence/` | Test run | After implementation |

---

## Feedback Loop

As you use the new framework:

1. **For Issue Authors**: If you think the template is missing something, note it in handoff
2. **For Implementers**: If something is unclear during implementation, update the issue or handoff
3. **For Reviewers**: If you find gaps in spec/evidence during review, flag them
4. **Update This Guide**: Every two issues, revisit this template and incorporate learnings

---

## Summary

The new framework helps by:
- **Eliminating ambiguity**: Phases 1/2/3 are explicit; boundaries are clear
- **Reducing handoff friction**: Standard template structure; no improvisation needed
- **Scaling documentation**: Each piece lives in one place; easy to update
- **Clarifying completion**: Not done until all phases pass; no gray areas

**Next issue using this template should feel smoother.**

---

## References

- [Handoff Template](handoff-template.md) — Standard structure for all handoffs
- [Completion Standards](completion-standards.md) — Phases 1/2/3 definitions
- [Issue Documentation Policy](issue-documentation-policy.md) — Where information lives
