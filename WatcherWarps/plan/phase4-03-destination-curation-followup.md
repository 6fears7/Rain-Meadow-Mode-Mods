# Phase 4.3 — Corrupted-destination curation (deferred content task)

Parent: [phase4-00-overview.md](phase4-00-overview.md). Explicitly deferred by the parent plan
(line 39: "Building the curated list itself is a content task, not a code task — flag it as
follow-up work once the mechanical pieces above are in place"). This subtask exists to hold that
follow-up, not to be done now.

## What this is

Once [[phase4-01-injection-mechanism-investigation]] and [[phase4-02-corrupted-warpdata-preset]]
land, the mechanism can inject a fixed-destination, bad-warp-cosmetic `WarpPoint` into any room
this mod chooses. What's still undecided:

1. **Which rooms count as "corrupted" destinations** — thematically, a failed/bad warp should land
   somewhere that reads as off-the-beaten-path, dangerous, or narratively "wrong" (vanilla's own
   `levelBadWarpTargets` list, referenced at
   [plan/reference/Watcher.WarpPoint.decompiled.cs:1286,1391,1429](reference/Watcher.WarpPoint.decompiled.cs#L1286),
   is the vanilla precedent for what counts — worth reading as a starting reference list even
   though this plan avoids routing through the live RNG roll itself).
2. **Which rooms get the injected warp point** — i.e., where a player would actually encounter a
   "you might get sucked into a bad warp" opportunity. This could be every ordinary `WarpPoint` (a
   chance/flavor addition), a dedicated small set of rooms, or something else — a design call, not
   an engineering one.

## Overlap with Phase 6 — don't duplicate

[Phase 6 of the parent plan](watcher-warping-support.md#phase-6--curate-real-destination-rooms)
already did the heavy analytical lift for region connectivity: it enumerated every Watcher region,
parsed all 84 placed `WarpPoint`s and 16 `SpinningTopSpot`/Echo objects across the whole game
install into [plan/watcher-warp-links.tsv](watcher-warp-links.tsv), and identified which regions
have zero fixed connectivity (`wdsr`, `wgwr`, `whir`, `wsur`) versus self-contained/scripted areas
needing individual verification (`waua`, `wara`). That analysis is about *normal* reachability
gaps, not "corrupted" flavor destinations, but the same TSV and region list are the right starting
point here too — re-derive connectivity data from scratch only if the mod's target game version
changes (per the TSV's own regeneration note). Don't re-run the room-data grep independently.

## Why this stays deferred

- It needs the mechanism (subtasks 1-2) actually working and tested in-game first — there's no
  way to validate "does this room read as a good corrupted destination" without being able to
  actually warp there.
- It's a design/level-curation decision, not something to pre-decide in a planning pass — matches
  how [[phase3-03-warpfilter-scope-decision]] was also left as an explicit decision point rather
  than folded into that phase's code fix.

## Note for next session

Updated 2026-08-26 (item 1 done): Item 1 ("which rooms count as corrupted destinations") is
resolved with real data, not a guess — `src/CorruptedWarpDestinations.cs` holds vanilla's actual
`levelBadWarpTargets` list, derived by grepping every `mods/watcher/world/*-rooms/*_settings.txt`
for `DynamicWarpTarget` placed objects with the trailing `badWarp` field `true` (field layout:
`<x><y><handle1~handle2~rippleLevelRequirement~deadEnd~badWarp>`, confirmed against
`RainWorld.cs`'s room-scan cache logic via `ilspycmd`), excluding `wora`-prefixed rooms the same
way vanilla's own `ChooseDynamicWarpTarget` does (decompiled.cs:1393). Result: 7 rooms — `wdsr_a07`,
`wdsr_a19`, `wgwr_a08`, `whir_a18`, `whir_b07`, `whir_c04`, `wsur_a40` — all inside the four
zero-fixed-connectivity gap regions from Phase 6's TSV, which fits the "off-the-beaten-path"
theming well.

Updated 2026-08-26 (item 2 done): Item 2 is now decided (user call, not inferred): **20% of the 84
real `WarpPoint` source rooms**, chosen deterministically (not runtime RNG — see
[[phase4-02-corrupted-warpdata-preset]]'s reasoning for avoiding per-client RNG-seed sync), via a
reproducible md5-hash sort documented in `src/BadWarpLinks.cs` — 17 source rooms, each round-robin
assigned one of the 7 `CorruptedWarpDestinations` entries.

Also per this round's instruction, **every bad-warp link is two-way**: `CorruptedWarpInjectionHooks`
now injects into both the source room (warp to destination) and the destination room (a separate
warp back to that specific source), rather than the earlier one-way-only design. Both legs still set
`oneWay=true`/`Data.oneWayExit=true` internally — not because either leg is meant to be one-way, but
because that's what bypasses `TrySpawnWarpPoint`'s non-dynamic destRoom-must-already-be-referenced
gate (no authored content backs either side of an injected link). Functionally the pair is
bidirectional. A destination room can receive more than one source's return leg (round-robin cycles
7 destinations across 17 sources) — `TrySpawnWarpPoint` dedups by `destRoom`, so each distinct return
leg still spawns as its own `WarpPoint` rather than colliding.

`src/Warps.cs`'s existing `BuildCorruptedWarpPlacedObject(destRegion, destRoom)` needed no changes —
it's called symmetrically for both legs of each link.

**Still not tested in-game** — someone needs to load a Meadow Watcher-timeline lobby and check at
least one full round trip (e.g. `wska_d27` → `wdsr_a07` → back) to confirm both injected warp points
spawn, are visible/triggerable, and the return leg actually lands back in the dedicated source room
through Phases 1-3's pipeline.

Updated 2026-08-27 (dedicated source rooms): Per user decision, a corrupted forward leg no longer
shares a room with a real vanilla `WarpPoint`. `BadWarpLinks.All`'s `SourceRegion`/`SourceRoom`
fields now point at one dedicated in-region room per region (list + selection criteria in
`src/BadWarpLinks.cs`'s class comment), chosen from the installed Watcher world files: quiet
dead-ends, corruption-themed where the region has that theming (whir/wgwr/wdsr/ward), never a
WarpPoint / Echo (SpinningTopSpot) / merchant / collectible room. WHIR (3 legs) and WMPA (2 legs)
share one dedicated room each; `CorruptedWarpInjectionHooks.SpreadPos` now fans co-located
injections out horizontally (±30 world units per index) so trigger volumes don't stack — this also
helps destination rooms that receive multiple return legs. Return-leg injection targets update
automatically (same `Link` fields). Room picks are unvalidated in-game — walkability of the
`ReachablePos` anchor and the fanned offsets in each of the 14 rooms still needs a visual check.
