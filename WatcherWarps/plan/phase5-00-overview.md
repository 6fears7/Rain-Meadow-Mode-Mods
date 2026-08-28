# Phase 5 — Gate the feature on the Watcher timeline, not on region access — Overview

Parent plan: [watcher-warping-support.md](watcher-warping-support.md) (Phase 5 section, lines
41-44).

## Correction to the parent plan's framing

The parent plan frames Phase 5 as a single gating decision still to be made: "require the Meadow
lobby to be running with Watcher as its timeline before any warp behavior activates at all." In
practice this has already happened, piecemeal, as each of Phases 1-4 was implemented — every
warp-related hook written so far independently checks the same condition before doing anything:

```csharp
OnlineManager.lobby?.gameMode is MeadowGameMode
    && OnlineManager.lobby.meadowTimeline == WatcherEnums.SlugcatStatsName.Watcher.value
```

Confirmed present (inline or via a local `IsMeadowWatcher()` helper) in all four existing hook
files: `src/ProgressionFilterGuardHooks.cs`, `src/CreatureWarpState.cs`,
`src/PerformWarpStripHooks.cs`, `src/CorruptedWarpInjectionHooks.cs`. So the mechanical
"is-it-gated" question is already answered yes — Phase 5 is not "write the gate," it's three
smaller loose ends the parent doc bundled under that heading:

1. The gate condition is copy-pasted four times with no shared helper — worth a decision on
   whether to consolidate now that a fourth copy has landed.
2. The gate reads `OnlineManager.lobby.meadowTimeline`, a value this mod doesn't set — it's
   assigned during lobby creation by Rain Meadow's own `Menu/LobbyCreateMenu.cs` and synced via
   `Lobby.LobbyState.timeline` (per the parent doc's Phase 5 paragraph and Critical Files list).
   Whether it's guaranteed populated by the time `Room.Loaded`/`RoomSettings` construction (i.e.
   before any of this mod's hooks fire) has not actually been checked against that sibling repo's
   code — only assumed.
3. The parent doc's own follow-up ("if some corrated room turns out to sit in a region Watcher's
   `SlugcatStats` access list doesn't include, revisit `EstablishWorlds`") is unverified and
   explicitly blocked on Phase 6/Phase 4-03's destination curation landing first.

None of these three needs new hook-style investigation the way Phases 1/3/4 did — there's no
"is it hookable" question here, since nothing new is being hooked. These are a small consolidation
task, a read-only verification task, and a deferred verification task.

## Subtask sequence

1. [[phase5-01-guard-consolidation-decision]] — decide whether to factor the four duplicated
   `MeadowGameMode` + `meadowTimeline == Watcher` checks into one shared helper (e.g. a static
   `Warps.IsMeadowWatcher()` alongside the existing `Warps` class) and do the mechanical rename if
   so. Independent, ready to execute now — no dependency on the other two subtasks.
2. [[phase5-02-lobby-timeline-assignment-verification]] — read-only investigation (against the
   sibling `/home/preston/repos/Rain-Meadow` repo's `Menu/LobbyCreateMenu.cs` and
   `Online/Resource/Lobby.cs`, per this plan's own Critical Files list) confirming
   `OnlineManager.lobby.meadowTimeline` is populated before `Room.Loaded`/`RoomSettings`
   construction can run for any client in the lobby, so the existing guard never reads a
   not-yet-assigned value. Independent of subtask 1.
3. [[phase5-03-region-access-verification]] — for each curated `destRegion` in Phase 4's bad-warp
   links and Phase 6's region-graph list, confirm it resolves under Watcher's `SlugcatStats`
   region-access rules (`overworldSession.overWorld.regions`); flag any that don't as needing an
   `EstablishWorlds` override. Explicitly blocked until Phase 6/phase4-03's curated destination
   list is finalized — do not start this before then.

## Note for next session

Created 2026-08-26: Phase 5 broken into the three subtasks above after finding the parent doc's
framing was already satisfied piecemeal by Phases 1-4's independent guards.

Updated 2026-08-26 (later): [[phase5-01-guard-consolidation-decision]] and
[[phase5-02-lobby-timeline-assignment-verification]] are both done — `Warps.IsMeadowWatcher()` is
now the single shared guard used by all four hook files, and the user confirmed
`meadowTimeline` is guaranteed populated before any room load, so no race condition exists. Only
[[phase5-03-region-access-verification]] remains, and it's still correctly blocked on Phase 6 /
[[phase4-03-destination-curation-followup]]'s curated destination list landing.

Updated 2026-08-26 (later still): [[phase5-03-region-access-verification]] is done — and it found
a real bug, not a clean bill of health. `WDSR`/`WGWR`/`WHIR`/`WSUR` (all four Phase 6 gap regions,
which are also every `CorruptedWarpDestinations` room) are in Watcher's *optional*-region list, not
its story-region list (confirmed via `ikdasm` against the installed `Assembly-CSharp.dll`), so
`EstablishWorlds` never establishes worlds for them in a Meadow sandbox — the timeline gate alone
is *not* sufficient, contrary to the parent plan's working assumption. Fix is split into
[[phase5-04-establishworlds-gap-region-override]], not yet started. Phase 5 overall is not done
until that lands.

Updated 2026-08-27: [[phase5-06-null-warps-to-daemon]]'s Subtask 5.6.1 (verify `WRSA`/Daemon is
establishable) is done. `WRSA` is in **neither** `SlugcatStoryRegions` nor `SlugcatOptionalRegions`
for any slugcat (one notch worse than the gap regions), but is unconditionally in the Watcher
`World/regions.txt`. More importantly it exposed that `EstablishWorldsHooks` reads
`Region.WarpRegions`, which is `null` at `EstablishWorlds` time in a Meadow lobby — so phase5-04's
override currently establishes nothing. New blocker [[phase5-07-establishworlds-warpregions-null-fix]]:
switch the hook to `overworldSession.overWorld.regions` and re-check phase5-04's premise at runtime.
Phase 5 is not done until 5.4 **and** 5.7 land.

Updated 2026-08-27: [[phase5-07-establishworlds-warpregions-null-fix]] **resolved** — option 1.
Disassembly (`ikdasm`) proved `EstablishWorlds` iterates only `overWorld.regions`, which
`OverWorld..ctor` fills from the unfiltered mod-merged `World/regions.txt`, and that file lists
every curated region (`WHIR WSUR WDSR WGWR WSSR HI SU CC SH LF WRSA`). So the base loop already
establishes all of them; phase5-04's "optional regions never established in a sandbox" premise
was a misdiagnosis (the story/optional split governs region-graph access, not `EstablishWorlds`).
`EstablishWorldsHooks` rewritten to read `overWorld.regions` and is now a correct-but-inert
safety net that also logs the region list for a final in-game smoke test (5.7 step 4, the only
open item). Phase 5 is effectively complete pending that one in-game check.

Updated 2026-08-27: [[phase5-04-establishworlds-gap-region-override]] is **implemented**
(`src/EstablishWorldsHooks.cs`, Harmony postfix on `OnlineGameMode.EstablishWorlds`). Scope was
widened per user instruction from the 4 gap regions to all 9 Watcher optional regions
(`WHIR WSUR WDSR WGWR WSSR HI SU CC SH`) — curated warp routes traverse the base-game overlay
regions (`CC` etc.) too, and those are only reached via spinning top / Echo in vanilla. Region
numbers resolved from `Region.WarpRegions` (the same array `EstablishWorlds` reads), no guessing.
Builds clean; still needs one in-game check that a warp can actually *complete* into a
force-established optional region. A new follow-up, [[phase5-05-optional-region-warp-points]], was
spun off for the content work of placing reachable warp points *into* `CC`/`HI`/`SU`/`SH`/`WSSR`
(the gap regions already get theirs from `BadWarpLinks`/`CorruptedWarpInjectionHooks`).
