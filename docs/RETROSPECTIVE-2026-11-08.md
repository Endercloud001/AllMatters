# Retrospective: AllMatters #80 Implementation & Issues Linking
**Date**: 2026-11-08  
**Sessions Analyzed**: 
- Main session (local_6399a17e-81c5-4f04-9a8b-47270ac7912a)
- #80 两轴代码审查 (local_8805c4fc-5e4b-4c05-89b4-26429fcbd3c3)
- #80 修复 + PR 合并 (local_9ed5221f-1e2f-4eee-9955-68219c0a4ec8)
- GitHub Issues 依赖关系补足 (local_7905ad8c-45c8-4b11-8f71-2c7a73e68c07)
- 修正 Issue 循环引用 (local_7a3096b3-fae8-4d42-899a-d7a131c1e8f8)

---

## 🔴 Critical: No Automated Guardrails

**Finding**: The AllMatters repository has **zero automated checks** running on commits or CI.

**Evidence**:
- No active pre-commit hooks (all `.sample` files in `.git/hooks/`)
- No `.github/workflows/` directory (no CI configuration)
- No linting, type-checking, or test automation gates
- Manual two-axis code review performed by subagent — 660+ seconds, 463k tokens spent

**Impact**:
- #80 implementation introduced 3 critical edge-case issues (ConditionalWeakTable cleanup, TaskCompletionSource lifecycle, Reset completion) that survived to PR merge
- Standards-axis review detected these post-facto, costing 463k tokens
- No mechanical enforcement of C# naming conventions, Godot patterns, or memory safety

**Recommendation**: **Install guardrails before continuing #81 or later issues**

### Immediate Actions (Priority Order)

1. **Pre-commit hook for basic sanity**:
   ```bash
   # .git/hooks/pre-commit (chmod +x)
   #!/bin/sh
   dotnet build --no-restore || exit 1
   # Add: dotnet test when test suite exists
   ```

2. **CI workflow** (`.github/workflows/ci.yml`):
   ```yaml
   name: CI
   on: [push, pull_request]
   jobs:
     build:
       runs-on: windows-latest
       steps:
         - uses: actions/checkout@v4
         - uses: actions/setup-dotnet@v3
         - run: dotnet restore
         - run: dotnet build --no-restore
         - run: dotnet test --no-build  # when tests exist
   ```

3. **Linting rules**: Add `.editorconfig` for C# conventions (see `docs/agents/review.md` mechanical rules → deterministic checks)

**Cost/Benefit**: 
- Setup: <30 minutes
- Savings: 400k+ tokens per issue (avoid post-facto 2-axis reviews)
- Risk reduction: Catch crashes, null-refs, and lifecycle bugs before merge

---

## 🟡 Major: GitHub Issues Process Gaps

**Finding**: New issues (#81, #91-99) created without following Matt Skills management flow.

**Evidence** (from dependency补足 session):
- Issues lack `ready-for-agent` labels
- No proper GitHub sub-issue relationships (only description text links)
- Dependency structure required manual `gh issue edit` cleanup (3 issues had circular/stale references)
- #95-98 scope overlaps with existing #76-88 (e.g., #95 "Map engine" vs #77 "T04 exploration")

**Impact**:
- Agents cannot self-discover work (no `ready-for-agent` filter)
- Manual triage required before each issue start
- Duplicate work risk (overlapping scopes between issue series)

**Recommendation**: **Standardize issue creation workflow**

### Issue Template Checklist

Before creating new issues, run:
```bash
# 1. Check for duplicates
gh issue list --state all --search "in:title,body <keywords>"

# 2. Use Matt Skills labels on creation
gh issue create --label "ready-for-agent" --label "T15" \
  --body "## Blocked by\n- #76\n\n## Dependencies\n..."

# 3. Link via GitHub native relationships (not just description text)
gh issue edit 91 --add-project "AllMatters" --add-assignee "@me"
```

### Process Update

Add to `docs/agents/issue-tracker.md`:
```markdown
## Creating New Issues

1. **Search for duplicates** (`gh issue list --search`)
2. **Apply Matt labels** (`ready-for-agent`, triage priority)
3. **Link dependencies** via GitHub UI or `gh issue edit`
4. **Verify spec hierarchy**: Parent epic → phase issues → sub-tasks
5. **Run ReviewScope.ps1** if touching existing gameplay
```

---

## 🟢 Minor: Permission Friction on Read-Only Operations

**Finding**: Multiple sessions requested approval for `gh issue view`, `gh issue list`, and JSON parsing scripts.

**Evidence** (from transcripts):
- Dependency补足 session: 20+ `gh issue view` calls, each with approval card in default mode
- Issue修正 session: 3x `gh issue view` + 2x `jq` parsing, approval required

**Current State**: Default permission mode treats all `gh` commands equally.

**Recommendation**: **Adjust permission rules for read-only GitHub queries**

### Option A: Use Auto Mode for GitHub-Heavy Work
- Sessions focused on issue management run in Auto mode
- Main coding sessions stay in Default mode
- Trade-off: Broader auto-approval scope

### Option B: Granular Permission Rule (if supported)
- Whitelist read-only `gh` subcommands: `gh issue list`, `gh issue view`, `gh pr view`
- Keep write operations (`gh issue create/edit`, `gh pr create/merge`) in approval gate
- Requires: Check if Claude Code supports per-command permission rules

**Cost/Benefit**:
- Saves: ~15 approval interruptions per GitHub-heavy session
- Risk: Minimal (read-only ops cannot modify repo or issues)

---

## 🟢 Minor: Browser Preview Unavailable in sts2mod Directory

**Finding**: Browser tools enabled globally but unavailable in sessions launched from `sts2mod` parent directory.

**Evidence**:
- Main session working directory: `E:\...\sts2mod` (not `E:\...\sts2mod\AllMatters`)
- `preview_start` rejected with "in-app Browser preview is not enabled on this install"
- User confirmed Browser tools toggle is ON in Settings

**Root Cause**: Browser tools permission is per-working-directory, not global.

**Recommendation**: **Always launch AllMatters sessions from the `AllMatters` subdirectory**

Add to session start checklist:
```bash
# In Claude Code: File → Open Folder → AllMatters (not sts2mod parent)
# Or: Use "Change Directory" on existing session
```

Alternative: If sessions must start in `sts2mod`, immediately call:
```tool
mcp__ccd_directory__change_directory({ path: "E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters" })
```

---

## 📊 Token Economy: RTK Usage

**Finding**: RTK available but not consistently used across subagent sessions.

**Evidence**:
- Global `CLAUDE.md` contains full RTK instructions (132 lines)
- Git log showed RTK warning: "No hook installed — run `rtk init -g`"
- Transcripts show mix of `rtk git` and plain `git` commands

**Impact**: Missed 59-80% savings on git operations (status, log, diff, add, commit).

**Recommendation**: **Install RTK hook and audit subagent prompts**

1. Install RTK hook:
   ```bash
   rtk init --global
   ```

2. Add to AGENTS.md:
   ```markdown
   ## Tool Economy
   
   Prefix all shell commands with `rtk` (see ~/.claude/CLAUDE.md). 
   Examples: `rtk git status`, `rtk gh issue list`, `rtk npm install`.
   ```

3. Verify subagents receive RTK instructions (check session spawn prompts)

---

## 🔍 Navigation: No Pointer to Review.md from AGENTS.md

**Finding**: Two-axis review agent re-implemented standards checks that already exist in `docs/agents/review.md`.

**Evidence**:
- Review session spent 463k tokens, 660s on full spec+standards analysis
- `docs/agents/review.md` exists (68 lines, standards rules for C#/Godot)
- `AGENTS.md` line 25 mentions review.md for "审查时", but subagent didn't load it

**Root Cause**: Weak pointer wording — "审查时读取" doesn't trigger for "two-axis review" invocation.

**Recommendation**: **Sharpen the review.md pointer in AGENTS.md**

Current (line 6):
```markdown
开始工单或准备 PR 时运行 `tools/ReviewScope.ps1`；审查时读取 `docs/agents/review.md`。
```

Revised:
```markdown
开始工单或准备 PR 时运行 `tools/ReviewScope.ps1`。**Code review** (spec-axis, standards-axis, two-axis): load `docs/agents/review.md` first for standards rules, then apply spec checks.
```

Front-load "Code review" as leading word; expand trigger to match all review types.

---

## 📋 Pre-Work Checklist for #81 and Beyond

Before starting #81 (multiplayer exploration & lobby recovery):

### 1. Install Guardrails (30 min)
- [ ] Create `.git/hooks/pre-commit` with `dotnet build` check
- [ ] Add `.github/workflows/ci.yml` for PR checks
- [ ] Run `rtk init --global` to install token-saving hook

### 2. Verify Issue Quality (10 min)
- [ ] Confirm #81 has `ready-for-agent` label
- [ ] Check #91-94 dependencies are correct (no circular refs)
- [ ] Verify no scope overlap with #76-88

### 3. Session Setup (5 min)
- [ ] Launch session from `AllMatters` directory (not `sts2mod`)
- [ ] Confirm Browser tools available (`preview_list` succeeds)
- [ ] Set permission mode: Auto for GitHub-heavy work, Default for coding

### 4. Review Workflow (before coding)
- [ ] Read `docs/agents/review.md` for standards rules
- [ ] Run `tools/ReviewScope.ps1` to identify affected domains
- [ ] Load relevant `docs/adr/` if design changes needed

---

## Summary: Severity Ranking

| Finding | Severity | Est. Fix Time | Token Impact |
|---------|----------|---------------|--------------|
| No automated guardrails | 🔴 Critical | 30 min | -400k/issue |
| GitHub Issues process gaps | 🟡 Major | 15 min + workflow doc | -50k/issue |
| Permission friction (read-only ops) | 🟢 Minor | Config change | -10k/session |
| Browser preview directory issue | 🟢 Minor | Session launch habit | -5k/session |
| RTK not consistently used | 🟢 Minor | `rtk init -g` | -20k/session |
| Review.md pointer not firing | 🔍 Navigation | 1 line edit | -460k/review |

**Total potential savings per issue**: ~500k tokens + faster feedback loops.

---

**Next Action**: User decides whether to install guardrails now or proceed with #81 as-is.
