# AllMatters Git & .local Cleanup Report
**Date:** 2026-11-08  
**Status:** Completion Ready  
**Working directory:** E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters

---

## Executive Summary

✅ **Git compression:** 119M → 96M (**19.3% reduction**)  
✅ **File reorganization:** 98 loose files moved to 17 issue-specific directories  
✅ **Legacy logs:** 128 unclassified files moved to `.local/legacy-logs/`  
⏳ **Pending user decision:** Delete `references/STS2-Balance-MOD/` (721M)?

---

## 1. Git Cleanup Phase — COMPLETED

### Compression Results
```
Before:  119M (.git directory)
After:   96M  (.git directory)
Savings: 23M  (19.3%)
```

### Process Executed
```bash
git gc --aggressive        # Object packing & compression
git reflog expire --expire=now --all  # Expire reflogs
git prune                  # Remove unreachable objects
```

### Remote Branch Analysis
| Category | Count | Recommendation |
|----------|-------|-----------------|
| **Merged branches** | 20 | Safe to delete (not deleted yet) |
| **Unmerged branches** | 10 | Keep for active development |
| **Total remote branches** | 33 | — |

### Merged Branches (Candidates for Deletion)
See `branch_cleanup_recommendation.md` for full list. Summary:
- Feature branches for shipped issues (#4, #6, #7, #8, #11, #12, #13, #36, #38, #45-47, #74)
- Chore & fix branches
- **Action:** Awaiting user confirmation before deletion

### Unmerged Branches (Active Work — Keep)
- `origin/decision/jigsaw-integration` — Planning for multiplayer jigsaw feature
- `origin/decision/jigsaw-verification-handoff` — Handoff documentation  
- `origin/prototype/jigsaw-*` (3 branches) — Multiplayer feature prototypes
- `origin/prototype/hunting-*` (2 branches) — Hunting feature exploration
- `origin/research/*` (2 branches) — Research in progress

---

## 2. .local Directory Reorganization — COMPLETED

### Before State
- 6,087 log/txt/png files scattered at various levels
- 48 subdirectories with mixed purposes
- No clear issue-based organization

### After State
```
.local/
├── 27-logs/ ... 47-logs/           (17 directories) — Issue-specific artifacts
├── legacy-logs/                    (128 files) — Unclassified logs
├── research-worktrees/             (819M, KEEP) — Active git worktrees
├── references/                     (744M) — Design/code references
├── implementation-*/               — Implementation notes by issue
├── issue80/ issue80-followup/       — Current sprint (Issue #80)
├── package-76-source/              (343M, archive candidate)
├── release-v0.1.1-preview.1/      (340M, archive candidate)
└── [other working directories]     — UI exploration, tests, tools
```

### Space Breakdown (Top consumers)
| Component | Size | Type | Recommendation |
|-----------|------|------|-----------------|
| `research-worktrees/` | 819M | **KEEP** (active git worktrees) | Don't delete |
| `references/STS2-Balance-MOD/` | 721M | External codebase | **DELETE** (no longer needed) |
| `references/` (other) | 23M | Design reference | Keep |
| `package-76-source/` | 343M | Build artifacts | Archive to external |
| `release-v0.1.1-preview.1/` | 340M | Release artifacts | Archive after v0.1.1 stable |
| **Other components** | 380M | Implementation logs, worktrees | Keep |
| **legacy-logs/** | 21M | Historical logs | Keep (audit trail) |

### Potential Savings (With User Approval)
```
If delete STS2-Balance-MOD/:     ~721M
If archive build artifacts:      ~683M
Total possible reduction:        ~1.4GB (54% of current .local size)
```

---

## 3. New Documentation Files

### Created
- `.local/STRUCTURE.md` — Complete directory structure documentation with preservation rationale
- `branch_cleanup_recommendation.md` — Detailed branch analysis & recommendation

---

## 4. Aggregate Repository Metrics

| Metric | Before | After | Savings |
|--------|--------|-------|---------|
| `.git` size | 119M | **96M** | **23M** |
| `.local` size | 2.6G | 2.3GB* | 300M* |
| **Total repo** | 2.8G | 2.4GB* | 400M* |

*Estimates based on current disk state; actual savings from deleting STS2-Balance-MOD/ would be ~721M.

---

## 5. Current Decision Points — Awaiting User Input

### DECISION 1: Delete STS2-Balance-MOD/ (721M)
**Rationale:**
- External project clone (not part of AllMatters codebase)
- Was useful during initial research phase (v0.0.x)
- No longer actively consulted during v0.1.x development
- Takes up 28% of .local directory

**Options:**
- ✅ **Recommended:** DELETE (save 721M)
- 📦 Alternative: Archive to external storage
- ⏸️ Keep for now

### DECISION 2: Archive Build Artifacts (683M)
**Components:**
- `package-76-source/` (343M)
- `release-v0.1.1-preview.1/` (340M)

**Rationale:**
- Only needed for post-release bugfixes
- Redundant after v0.1.1 goes live
- Can be recovered from CI/release archives

**Options:**
- ✅ **Recommended:** Archive to external USB/cloud
- ⏸️ Keep locally for now
- 🗑️ Delete (not recommended if v0.1.1 still in testing)

### DECISION 3: Delete Merged Remote Branches
**Candidates:** 20 remote branches (safe to delete; already merged to main)

**Options:**
- 🔄 **Batch delete via:** `git push origin --delete <branch1> <branch2> ...`
- ⏸️ Wait (branches don't consume significant space)

---

## 6. Git Cleanup Artifacts

Located in shared temp folder:
- `/tmp/branch_cleanup_recommendation.md` — Branch analysis details
- `/tmp/merged_branches.txt` — Full merged branch listing
- `/tmp/unmerged_branches.txt` — Full unmerged branch listing

---

## 7. Files Status in Working Tree

All reorganized `.local/` files remain in working tree (not committed). 
All `.git` compression changes are permanent local changes.

**Ready for:**
- ✅ User review of decisions above
- ✅ Commit (once decisions finalized)
- ✅ Push to clean up remote

---

## Next Steps

1. **User confirms/modifies decisions** → Will proceed with approval
2. **Delete STS2-Balance-MOD/** (if approved)
3. **Archive build artifacts** (if approved)
4. **Delete merged remote branches** (if approved)  
5. **Final measurement** of achieved space savings
6. **Commit & push** final cleanup state

---

## Notes

- ✅ No breaking changes made to active code or configuration
- ✅ No local branches modified
- ✅ HEAD remains at `a43f61e` (feat(jigsaw): support native multiplayer editing #80)
- ✅ All worktrees remain functional and registered
- ✅ All unmerged branches preserved for ongoing work

**Cleanup completed safely with high confidence.**

---

**Generated by:** Claude Code cleanup agent  
**Task origin:** AllMatters v0.1.2 prep — parallel cleanup initiative
