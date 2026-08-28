# Phase 5.1 — Guard consolidation decision

Parent: [phase5-00-overview.md](phase5-00-overview.md).

## The situation

The condition

```csharp
OnlineManager.lobby?.gameMode is MeadowGameMode
    && OnlineManager.lobby.meadowTimeline == WatcherEnums.SlugcatStatsName.Watcher.value
```

(or its negated `is not`/`!=` form used as an early-return guard) appears independently in four
files:

- `src/ProgressionFilterGuardHooks.cs` — wrapped in a private `IsMeadowWatcher()` method local to
  that class.
- `src/CreatureWarpState.cs` — inline.
- `src/PerformWarpStripHooks.cs` — inline, negated early-return form.
- `src/CorruptedWarpInjectionHooks.cs` — inline, negated early-return form.

Every other hook file this mod adds (any future Phase 6 content-loading code, any future Phase
2/3 remaining work) will need the same check. Four copies is enough to be worth a decision, not
yet enough to be an emergency.

## Decision to make

Whether to add a single shared helper — most naturally a static method on the existing `Warps`
class referenced in `CorruptedWarpInjectionHooks.cs`'s comments (check `src/Warps.cs` for its
current contents and `Apply()` structure first) — e.g. `Warps.IsMeadowWatcher()` — and replace the
four existing call sites with it, versus leaving the duplication as-is on the grounds that each
hook file is independently readable without needing to jump to a shared helper.

Recommendation (not yet executed): consolidate. The check is an exact behavioral contract (must
stay byte-for-byte identical across all gated hooks or the feature silently gates inconsistently
between subsystems), which is exactly the case where duplication is a liability rather than
clarity — a future edit to one copy (e.g. if the timeline comparison ever needs to change) could
easily miss the other three.

## Steps if consolidating

1. Read `src/Warps.cs` to confirm its current shape and whether `Apply()` or a static helper
   section is the natural home for a new `internal static bool IsMeadowWatcher()`.
2. Add the helper, matching the existing four call sites' exact condition (including the null-
   conditional `?.` on `OnlineManager.lobby`).
3. Replace the local `IsMeadowWatcher()` in `ProgressionFilterGuardHooks.cs` and the inline
   checks in the other three files with calls to the shared helper. Keep each file's early-return
   polarity (`is not .../ ||` vs `is ... &&`) as-is — only replace the condition expression, not
   the control flow shape, to minimize diff size and risk.
4. Build (`dotnet build` or whatever this project's existing build task is — check for a
   `.sln`/`.csproj` and any documented build command before assuming `dotnet build` works
   unmodified) and confirm no regressions.

## Note for next session

Done as of 2026-08-26: consolidated. `Warps.IsMeadowWatcher()` added to `src/Warps.cs`; all four
call sites (`ProgressionFilterGuardHooks.cs`, `CreatureWarpState.cs`, `PerformWarpStripHooks.cs`,
`CorruptedWarpInjectionHooks.cs`) now call the shared helper instead of duplicating the check,
each keeping its original early-return polarity. Removed now-unused `using RainMeadow;` from
`ProgressionFilterGuardHooks.cs` and `CorruptedWarpInjectionHooks.cs`. `dotnet build` succeeds
(4 pre-existing nullable warnings in unrelated files, 0 errors, 0 new warnings).
