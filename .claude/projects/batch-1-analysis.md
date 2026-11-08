# AllMatters Batch 1 Analysis

**Spec**: #73 (v0.1.2 Jigsaw Update)
**Tickets**: #77, #78, #79, #95

## Task Graph & Blockers

### Executable Now (Frontier)
- **#95** (Map engine & pathfinding) — No blockers, foundational infrastructure for exploration system

### Blocked by #76 (T03 - Not in this batch)
- **#77** (T04 探索) — Blocks #78, #79
- **#78** (T05 商店) 
- **#79** (T06 换幕)

Note: #76 (T03 mode entry, Ancient, single-player save) is **open** and not scheduled for this batch. All three T04–T06 tickets depend on T03 completion.

## Recommendation

**Option A**: Run #95 immediately in isolation, then pause until #76 completes
- Pro: Start infrastructure work now, parallel with #76
- Con: #77–79 remain blocked

**Option B**: Wait for #76 to complete first, then run all four in parallel
- Pro: Full batch cohesion, all work together
- Con: Delay start

## Integration Plan (Either Option)

1. Create `integration/batch-1` branch from main
2. Kick off implementers for available tickets (initially #95, later #77–79)
3. Merge each completed ticket to integration branch
4. After all complete: full code review on integration branch
5. Merge integration branch to main + close tickets

## Deliverables

- [ ] `dotnet build` passes
- [ ] Pre-commit hooks auto-format
- [ ] CI green (~30s)
- [ ] Code review (spec + standards) passes
