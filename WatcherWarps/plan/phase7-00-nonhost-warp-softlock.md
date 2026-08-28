# Phase 7 — Non-host clients can't use warp points (silent EnterWarp softlock)

Parent: [watcher-warping-support.md](watcher-warping-support.md). Found during the first
two-client playtest (2026-08-28).

## Symptom

On a **non-host** client, walking a non-Player avatar into an injected warp point plays the
trigger/white-out animation but never transitions — the avatar is frozen mid-warp, no error in
the BepInEx log. The **host** warps fine with the same avatar species and the same warp point.

## Root-cause chain (confirmed from logs, not guessed)

Non-host log: `BepInEx/LogOutput.log.1` (player `noble_pearl`, joined `cyan_vulture`'s LAN lobby).
Working host log for comparison: `BepInEx/LogOutput.log`.

1. Non-host log ends at `WatcherWarps: non-Player avatar triggered warp point, entering EnterWarp
   (triggerTime=200.32)` and shows **zero** `RainMeadow.EntityHooks.OverWorld_InitiateSpecialWarp_WarpPoint`
   lines (that hook logs `active world's name? …` unconditionally on every call). The host log
   has 13 of them. ⇒ **`OverWorld.InitiateSpecialWarp_WarpPoint` is never called on the non-host.**
2. The only path that calls it for our warps is `Watcher.WarpPoint.WarpPrecast()`
   (decompile line 1981/2017), invoked from `WarpPointTriggerHooks` when
   `canPreCast && Region.RegionReadyToWarp && !rippleEggWarpPoint`.
   - `Region.RegionReadyToWarp` is `true` (static, set by `Region..cctor`; only ever set `false`
     by an in-flight `LoadAllRegionsCoroutine`, which never ran here).
   - `rippleEggWarpPoint` is `false` for our data.
   - ⇒ `canPreCast` is `false`, **or** `WarpPrecast()` is entered but bails before its
     `InitiateSpecialWarp` call.
3. `WarpPrecast()` (decompile / IL): **first statement is `canPreCast = false`, before any
   guard.** Then it early-`ret`s if `warpWorldLoader != null && !Finished`, or if
   **`overWorld.activeWorld != room.world`** (IL_00c4–00e6). On a fresh session the loader is
   null, so the trip is **`overWorld.activeWorld != room.world`**.
   ⇒ First `WarpPrecast()` call latches `canPreCast = false` and starts nothing. The mod never
   re-armed `canPreCast` (it only does so when `triggerTime` decays to 0 — i.e. when you step
   away), so precast is dead for that warp point for as long as you stand in it.
4. `WarpPointTriggerHooks` then rode `triggerTime` past `triggerActivationTime` and called
   `ChangeState(State.EnterWarp)` **with no `warpWorldLoader` queued**. `WarpPoint.Update`'s
   EnterWarp body only leaves for ExitWarp once `warpWorldLoader.Finished` (decompile line 2419)
   → waits forever. Silent softlock.

## Fix applied (2026-08-28) — robustness layer, `src/WarpPointTriggerHooks.cs`

- Added `DestinationLoadStarted(WarpPoint)` — true once `overWorld.warpWorldLoader != null ||
  overWorld.warpingPreload || activeWorld.name == destRegion`.
- Precast block: only call `self.WarpPrecast()` when `!DestinationLoadStarted` **and**
  `overWorld.activeWorld == self.room.world` (so its internal guard passes and it can't latch
  `canPreCast=false` for nothing), re-arming `canPreCast = true` immediately before the call so
  it keeps retrying each tick.
- Commit block: **do not** `ChangeState(EnterWarp)` unless `DestinationLoadStarted(self)` — pin
  `triggerTime` at the threshold instead, and every 40 ticks log a warning dumping
  `activeWorld` / `room.world` / `sameRef` / `canPreCast` / `RegionReadyToWarp` /
  `warpWorldLoader`. Converts the softlock into a recoverable "stand there, nothing happens"
  plus the diagnostic needed for the open question below.

This fixes the case where `activeWorld` is merely *transiently* wrong (client still settling its
own world load when it first touches the warp). It does **not** fix a permanent mismatch — that
needs the investigation below.

## Open: why is `overWorld.activeWorld != room.world` on a non-host?

Both players started and stayed in SU the whole session; the non-host's avatar (a Big Needle,
switched from a Slugcat mid-session — `apo:0002` slot reused) is in `SU_C04`, and the warp point
is in `SU_C04`. `overWorld.activeWorld` and `room.world` should both be the one SU `World`.

Candidates to check, in order:

1. **Re-run the playtest with the fix** and read the new warning line. It prints
   `activeWorld=<name>` vs `room.world=<name>`. If both say `SU` it's a *different instance* of
   the same region's World (Meadow world-management bug / stale ref); if `activeWorld` is `NULL`
   or a different region, it's a client world-load-ordering bug.
2. **Avatar switch.** Does re-picking an avatar (Meadow `SpawnPlayers` re-entry, `sSpawningAvatar`)
   rebuild `RoomRealizer`/world refs on a client without updating `overWorld.activeWorld`? Grep
   `/home/preston/repos/Rain-Meadow` for avatar-reselection handling and `roomRealizer =`.
3. **Client initial load.** Confirm the non-host actually runs vanilla `OverWorld.WorldLoaded`
   (→ `activeWorld = world`) on join, not just `WorldSession.Activate()` +
   `AbstractRoom.RealizeRoom`. `Game/RainMeadow.LoadingHooks.cs:136-175` (`WorldLoader_Update`)
   and `:144` (`if (activeWorld == null) ForceLoadUpdate()`) suggest clients can transiently
   have `activeWorld == null`.
4. If `activeWorld` is a valid-but-wrong-instance SU World: the mod could fall back to calling
   `overWorld.InitiateSpecialWarp_WarpPoint(self, self.overrideData ?? self.Data,
   useNormalWarpLoader: false)` directly — but note `InitiateSpecialWarp` and
   `OverWorld.Update`'s warp-preload block both read `self.activeWorld` internally, so a wrong
   `activeWorld` may break the load anyway. Fixing/refreshing `activeWorld` is likely the real
   answer; check whether it's safe for the mod to set
   `overWorld.activeWorld = self.room.world` when they mismatch and the warp point's room is the
   locally-simulated one.

## Note for next session

Robustness fix is in but **unverified** — needs the two-client playtest repeated. The warning
line it emits is the key diagnostic for the open question above; capture the non-host log and
start from candidate 1. Everything else in Phases 1–6 is unchanged.
