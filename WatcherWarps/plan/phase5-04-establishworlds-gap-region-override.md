# Phase 5.4 — EstablishWorlds override for the four gap regions

Parent: [phase5-00-overview.md](phase5-00-overview.md). Spawned by
[[phase5-03-region-access-verification]]'s finding, not pre-planned — the parent plan explicitly
said to write this up as a new subtask rather than guess the fix during verification.

## The problem, precisely

[[phase5-03-region-access-verification]] confirmed (via `ikdasm` against the installed
`Assembly-CSharp.dll`, `SlugcatStats.SlugcatStoryRegions`/`SlugcatOptionalRegions`, `ModManager.Watcher`
branch) that `WDSR`, `WGWR`, `WHIR`, `WSUR` are in Watcher's *optional*-region list, not its base
story-region list. `OnlineGameMode.EstablishWorlds`
([GameModes/OnlineGameMode.cs:291-297](OnlineGameMode.cs#L291-L297)) only iterates
`overworldSession.overWorld.regions`, which is seeded from the story list at world-load time — a
Meadow sandbox lobby has no rot-progression trigger to ever add the optional regions afterward. So
every `CorruptedWarpDestinations` room (all 7 of them: `wdsr_a07`, `wdsr_a19`, `wgwr_a08`,
`whir_a18`, `whir_b07`, `whir_c04`, `wsur_a40`) and therefore every `BadWarpLinks` entry's
destination side currently points at a region with no established `WorldSession` — the warp would
fire but land nowhere valid.

## What needs deciding/built

The parent plan's own instruction (line 44 of `watcher-warping-support.md`) is explicit: revisit
`EstablishWorlds` for the *specific* regions that fail, not force-open every region. So the fix
scope is narrow:

1. A `MeadowGameMode`-scoped override (or hook on `EstablishWorlds`, matching the hook style already
   used throughout this mod — see `Warps.IsMeadowWatcher()` in `src/Warps.cs`) that, only when
   `IsMeadowWatcher()` is true, additionally establishes worlds for exactly `WDSR`, `WGWR`, `WHIR`,
   `WSUR` (the four confirmed-optional regions actually used by `CorruptedWarpDestinations`) —
   not the other 5 optional regions (`HI`, `SU`, `CC`, `SH`, `WSSR`) since nothing in this mod's
   curated content targets them.
2. Confirm `overworldSession.overWorld.regions`/`Region` objects for these four can actually be
   constructed on demand outside the normal rot-progression flow (i.e. that `Region.regionNumber`
   resolution and world-file loading for an "optional" region works when force-triggered, not just
   when the game's own rot state machine triggers it) — this needs either reading how the base game
   itself lazily adds optional regions (find the caller of `getSlugcatOptionalRegions`/
   `SlugcatOptionalRegions` at the other call sites turned up during phase5-03's `ikdasm` search,
   e.g. `/tmp`-transient IL search hit lines ~194844, ~2130759, ~2315811, ~2482441, ~2485653 in the
   disassembled `Assembly-CSharp.dll` — re-run `ikdasm` if that scratch file is gone) or an in-game
   empirical test.

## Done (2026-08-27)

Implemented in `src/EstablishWorldsHooks.cs`, wired into `Warps.Apply()`. Approach:

- **Harmony postfix on `RainMeadow.OnlineGameMode.EstablishWorlds(OverworldSession)`** (not an
  `On.` hook - that namespace only covers the game assembly, not Rain Meadow; matches
  `MeadowMounts/src/Mounts.cs`'s `harmony.Patch(AccessTools.Method(...))` style for patching Rain
  Meadow types). Guarded by `Warps.IsMeadowWatcher()`.
- **Scope widened past the original 4 gap regions** per user instruction: establishes worlds for
  **all 9 Watcher optional regions** (`WHIR WSUR WDSR WGWR WSSR HI SU CC SH`), because curated warp
  routes traverse the base-game overlay regions too (e.g. `CC`, reached in vanilla only via a
  spinning top / Echo, which is itself a `WarpPoint` under the hood and has no ordinary
  `WarpPoint`). Still far narrower than force-opening every region - all 9 are regions the Watcher
  timeline already lists, just as optional not story.
- **Region number resolution (item 2 above):** resolved empirically, not by reading rot-progression
  code. `OverWorld.regions` is just `Region.WarpRegions` (verified via `ikdasm`: `OverWorld.Update`
  sets `regions = Region.WarpRegions`, and `LoadAllRegionsCoroutine` builds `WarpRegions` from
  `World/regions.txt` with `regionNumber` = line index). `Region.WarpRegions` is fully populated at
  game-load time, long before an `OverworldSession` activates, so the hook reads the *same* `Region`
  objects `EstablishWorlds`' own loop uses - reusing `region.name` / `region.regionNumber` directly
  means no guessed shortID and no collision with the story regions vanilla already established.
  Optional regions still appear in `regions.txt` (they have world folders), they're just not in the
  `overWorld.regions` subset EstablishWorlds iterates - so `WarpRegions` still contains them.
- Skips any region already in `overworldSession.worldSessions` (defensive; covers the case where a
  future change adds one of these to the story set).

Build: `dotnet build` clean, no new warnings.

**Still unverified in-game** (needs a running Watcher-timeline Meadow lobby): that force-establishing
a `WorldSession` for an optional region actually lets a warp *complete* into it - i.e. that
`WorldLoader` / `OverWorld.GetRegion` construct the `World` fine outside the rot flow and
`LinkWorld` binds our pre-made session. High confidence it works (nothing rot-specific gates world
*construction*; the rot bit is room-setting templates), but it's the one thing that can only be
confirmed empirically. Covered by `watcher-warping-support.md` Verification step 7.

## Update 2026-08-27

The region array (renamed `WatcherOptionalRegions` → `ForceEstablishRegions`) gained `"LF"`
(Farm Arrays) per user instruction, so [[phase5-05-optional-region-warp-points]]'s `LF_B01W`
warp source has a synced `WorldSession`. `LF` is not a Watcher optional region — the comment in
`src/EstablishWorldsHooks.cs` documents the exception. If `Region.WarpRegions` lacks an `LF`
entry under the Watcher timeline the hook logs a warning and skips it.

## Correction 2026-08-27 (from [[phase5-06-null-warps-to-daemon]] Subtask 5.6.1)

The "Region number resolution" bullet above is **wrong**. `Region.WarpRegions` is *not* populated
at game-load — the `ikdasm` re-check for 5.6.1 found its only populators are
`Region.LoadAllRegionsCoroutine`, kicked solely from `OverWorld.InitiateSpecialWarp_WarpPoint`
(during a warp) and the story warp-preload path. `OverWorld..ctor` fills `OverWorld.regions` from
a *separate* non-coroutine `Region.LoadAllRegions` that never sets the static. `OverWorld.Update`'s
`regions = Region.WarpRegions` is gated on `warpingPreload && RegionReadyToWarp`.

⇒ `Region.WarpRegions` is `null` when `EstablishWorldsPostfix` runs (from
`OverworldSession.ActivateImpl`, before any warp), so this hook force-establishes **nothing**
today. Fix (read `overworldSession.overWorld.regions` instead; re-check whether the base loop
already covers these) is [[phase5-07-establishworlds-warpregions-null-fix]]. Also re-verify this
doc's core premise there — `OverWorld..ctor`'s `LoadAllRegions` has no visible timeline filter, so
`overWorld.regions` may already include the gap regions.

## Resolution 2026-08-27 (from [[phase5-07-establishworlds-warpregions-null-fix]])

This doc's core premise is **wrong**. `ikdasm` proved `EstablishWorlds` iterates only
`overWorld.regions`, filled by `OverWorld..ctor` from the unfiltered mod-merged
`World/regions.txt`, which lists every region in `ForceEstablishRegions`. The base loop already
establishes all of them; there is no optional-region gap at `EstablishWorlds` time. The hook
was rewritten in 5.7 to a correct-but-inert safety net. Only an in-game smoke test remains.

## Follow-up spun off

The user also flagged that `CC` (and the other traversed optional regions) have no ordinary
`WarpPoint` of their own - only a spinning top - so a curated warp *into* them still needs a
placed warp point. That's Phase 4/6 content work (source room + destination curation), not part of
this subtask. Written up as [[phase5-05-optional-region-warp-points]].
