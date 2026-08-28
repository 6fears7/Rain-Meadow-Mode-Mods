# Phase 5.7 — `EstablishWorldsHooks` reads `Region.WarpRegions` which is `null` at establish time

Parent: [phase5-00-overview.md](phase5-00-overview.md). Spun off 2026-08-27 from
[[phase5-06-null-warps-to-daemon]]'s Subtask 5.6.1.

## The bug

`src/EstablishWorldsHooks.cs::EstablishWorldsPostfix` resolves every `ForceEstablishRegions`
acronym (`WHIR WSUR WDSR WGWR WSSR HI SU CC SH LF WRSA`) with:

```csharp
Region? region = Region.WarpRegions?
    .FirstOrDefault(r => r != null && string.Equals(r.name, acronym, StringComparison.OrdinalIgnoreCase));
```

Verified against `Assembly-CSharp.dll` (`ikdasm`, 2026-08-27):

- `Region.WarpRegions` (static) is `null` and `Region.RegionReadyToWarp` is `false` from the
  static `.cctor`.
- `Region.WarpRegions` is populated **only** by `Region.LoadAllRegionsCoroutine` (`d__36`),
  which is started **only** from `OverWorld.InitiateSpecialWarp_WarpPoint` (during an in-progress
  warp) and the vanilla story warp-preload path.
- `OverWorld..ctor` sets `OverWorld.regions` from a *separate* non-coroutine
  `Region.LoadAllRegions(timeline, game)` that never touches the static.
- `OverWorld.Update` does `regions = Region.WarpRegions` but gated on
  `warpingPreload && Region.RegionReadyToWarp`.
- Rain-Meadow: 0 references to `WarpRegions` / `LoadAllRegions` / `RegionReadyToWarp`.
- `OnlineGameMode.EstablishWorlds` runs once from `OverworldSession.ActivateImpl`, at world
  activation — before any warp.

⇒ `Region.WarpRegions` is `null` when the postfix runs in a fresh Meadow lobby. Every entry
logs `region <X> not found in Region.WarpRegions` and **nothing is force-established**. All of
[[phase5-04-establishworlds-gap-region-override]] and [[phase5-06-null-warps-to-daemon]] is
currently inert. Contradicts phase5-04's stated "`Region.WarpRegions` is fully populated at
game-load time" — that populator was not found; re-check if you think it exists.

## Fix direction (decide, then implement)

Resolve against **`overworldSession.overWorld.regions`** — the exact array the base
`EstablishWorlds` loop already iterates, populated by `OverWorld..ctor` before activation — not
the static.

```csharp
Region? region = overworldSession.overWorld.regions?
    .FirstOrDefault(r => r != null && string.Equals(r.name, acronym, StringComparison.OrdinalIgnoreCase));
```

## Open question this fix must settle first (blocking)

`OverWorld..ctor`'s `Region.LoadAllRegions(timeline, game)` builds one `Region` per line of the
**active** `World/regions.txt` (resolved via `AssetManager.ResolveFilePath`) with **no visible
timeline/slugcat filter** in that method. The Watcher merged `regions.txt` lists all ~40 regions
including `WHIR WSUR WDSR WGWR WSSR WRSA`. So `overWorld.regions` may **already contain** these —
in which case:

1. the base `EstablishWorlds` loop *already establishes* WorldSessions for them, and
   phase5-03/phase5-04's "not in `overWorld.regions`, so never established" premise is wrong and
   needs a runtime re-check (log `overworldSession.overWorld.regions.Select(r => r.name)` in a
   live Watcher Meadow lobby); or
2. there *is* a filter (in `Region..ctor`, or `regions.txt` resolution is slugcat-specific, or
   `OverWorld` prunes later) — find it, then this hook still needs the region number from
   somewhere valid.

Do the one-line runtime log first. It decides whether this subtask is "swap the array" or
"phase5-04's whole diagnosis was off."

## Steps

1. [x] Instrument: log `overworldSession.overWorld.regions` names + numbers. Done statically
       instead of at runtime — see Resolution below. Hook now also emits this log line every
       activation for the in-game confirmation (step 4).
2. [x] Switched `EstablishWorldsHooks` to read `overworldSession.overWorld.regions`. Kept the
       `ContainsKey` guard and the whole curated loop as a defensive safety net — in the
       expected case it establishes nothing because the base loop already did.
3. [~] No filter to locate — there is none (see Resolution).
4. [ ] Rebuild done (`dotnet build`, clean). Still need in-game: trigger a redirected
       `NULL`-dest warp (e.g. `wska_d07`) and confirm arrival in `wrsa_c01` with a synced
       `WorldSession` and no `LinkWorld` "No WorldSession established" error. Check the
       BepInEx log for the `overWorld.regions = [...]` line and confirm no
       `region X not in overWorld.regions` warnings.

## Resolution (2026-08-27) — option 1: they're all already established

Settled the blocking open question by disassembly (`ikdasm` against the installed
`Assembly-CSharp.dll`) rather than a runtime log:

- `OnlineGameMode.EstablishWorlds` iterates `overworldSession.overWorld.regions` and nothing
  else — it never consults `SlugcatStats` story/optional access lists.
- `OverWorld..ctor` → `regions = Region.LoadAllRegions(PlayerTimelinePosition, game)`.
  `Region.LoadAllRegions(timeline, game)` reads `World/regions.txt` via
  `AssetManager.ResolveFilePath` (mod-merged, **not** timeline/slugcat filtered) and builds one
  `Region` per line, `regionNumber` = line index. No filter in that method, and the only
  Watcher branch in `Region..ctor` just tints corruption colors. The two other writes to
  `OverWorld.regions` (`LoadFirstWorld` path and `WorldLoaded`) also go through unfiltered
  `LoadAllRegions` / `Region.WarpRegions`.
- The active merged `World/regions.txt` with the Watcher mod enabled
  (`.../StreamingAssets/mergedmods/world/regions.txt`, mirrors
  `mods/watcher/modify/world/regions.txt`'s `[ADD]` lines) lists **all** of
  `WHIR WSUR WDSR WGWR WSSR HI SU CC SH LF WRSA`.

⇒ `overWorld.regions` already contains every curated region, so the base `EstablishWorlds`
loop already establishes a `WorldSession` for each. **phase5-04's premise was a
misdiagnosis** — phase5-03's optional/story split is real but governs region-graph *access*,
not `EstablishWorlds`. `Region.WarpRegions` being null at establish time (phase5-06's finding)
was also real but moot: the hook should never have read it.

## What was actually changed

`src/EstablishWorldsHooks.cs` rewritten: reads `overworldSession.overWorld.regions`, logs the
full region list once per activation (step 1 instrumentation, now permanent), and keeps the
curated `ForceEstablishRegions` loop purely as a safety net behind the `ContainsKey` guard. It
is expected to establish nothing. Builds clean, no new warnings.

## Note for next session

Only step 4 remains — an in-game smoke test. The diagnosis work is done and the hook is now
correct-but-inert. If the in-game log shows every curated region present with no warnings
(expected), consider deleting `EstablishWorldsHooks` entirely as dead code, or keep it as the
cheap safety net it now is. phase5-04 doc has a correction block pointing here.
