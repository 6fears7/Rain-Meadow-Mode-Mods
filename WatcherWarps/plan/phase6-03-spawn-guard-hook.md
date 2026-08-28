# Phase 6.3 — TimelineRegionGuardHooks

Parent: [[phase6-00-timeline-region-mismatch-spawn-guard]]
Depends on: [[phase6-01-hook-target-investigation]], [[phase6-02-region-validity-helper]]

## New file `src/TimelineRegionGuardHooks.cs`

- `Apply()` registered in `Warps.Apply()` (`src/Warps.cs:26-39`) alongside the others.
- Harmony **postfix** on the target(s) 6.1 settles on (expected: `MeadowMenu.StartGame`, plus
  `MeadowPauseMenu` resume path if one exists).

```csharp
private static void StartGamePostfix(MeadowMenu self)
{
    if (OnlineManager.lobby?.gameMode is not MeadowGameMode) return; // NOT IsMeadowWatcher()
    var room = self.manager.menuSetup.regionSelectRoom;
    if (string.IsNullOrEmpty(room)) return;
    if (Warps.RegionLoadableInCurrentTimeline(room)) return;

    Warps.Log?.LogWarning(
        $"Watcher Warps: saved spawn '{room}' is not in timeline "
        + $"'{OnlineManager.lobby.meadowTimeline}' region set; redirecting to Outskirts");
    Warps.RedirectSaveToOutskirts(self.manager);
}
```

## Notes

- Postfix (not prefix) so the vanilla assignment — including the dead-guard overwrite at
  `MeadowMenu.cs:349` — has already run and `regionSelectRoom` holds the final value.
- Must run before `RequestMainProcessSwitch(ProcessID.Game)` takes effect. That call is the last
  line of `StartGame`, and the process switch is deferred to end of frame, so a postfix is in
  time. Verify against `ProcessManager.RequestMainProcessSwitch` if in doubt.
- If 6.1 chooses a deeper chokepoint instead, the same body applies — just read
  `regionSelectRoom` off whichever `ProcessManager.MenuSetup` is in scope.
- Keep the existing-hook house style: `AccessTools.Method` + `MissingMethodException` on null
  target (see `src/EstablishWorldsHooks.cs:52-53`).
