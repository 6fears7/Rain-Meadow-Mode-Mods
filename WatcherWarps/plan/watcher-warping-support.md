# Watcher Warp Support in Meadow — Design Plan

## Context

Meadow's sandbox mode has no vanilla progression path between regions — karma gates are already unlocked unconditionally (`RegionGate_ctor` in [Meadow/RainMeadow.MeadowHooks.cs:275-282](Meadow/RainMeadow.MeadowHooks.cs#L275-L282)), but that only covers gate-adjacent regions. The Watcher DLC's `WarpPoint` system is the natural fit for letting a Meadow player reach *any* region (including ones normally only reachable via failed/"bad" warps) directly through a placed object, without inventing a new teleport mechanic from scratch. `PlacedObject.Type.WarpPoint` has already been added to Meadow's cosmetic allowlist ([GameModes/OnlineGameModeHelpers.cs:84](GameModes/OnlineGameModeHelpers.cs#L84)), so placed warp points now survive `Room_ctor`'s `FilterItems` pass and actually construct a live `Watcher.WarpPoint` instead of being deactivated.

However, three vanilla restrictions stand between "a warp point exists in the room" and "any Meadow avatar of any species can use it to reach any location":

1. **Vanilla warp triggering and the region/room swap are hardcoded to `Player`.** `Watcher.WarpPoint.SuckInCreatures()` only advances warp-trigger state (`activationTime`, `performingActivationTimer`, `warpPointCooldown`) for `isinst Player` checks; non-player creatures get pulled in cosmetically but never actually trigger a warp. Worse, `OverWorld.WorldLoaded()` (the method that actually swaps the active `World`/region) only ever relocates entries from `RainWorldGame.get_Players()` — any other `AbstractCreature` left in the old room is simply abandoned when the region swaps, because vanilla only ever expected one human-controlled `Player` per game.
2. **Warp availability is progression-gated.** `RippleLevelFilter`/`PrinceFilter`/`RippleEggFilter`/`WarpFilter` placed objects can deactivate a `WarpPoint` based on save-state ripple level, Prince-encounter count, or ripple-egg count — none of which make sense in a sandbox without that progression.
3. **Some vanilla locations are normally only reachable via a "bad warp"** (a corrupted/failed warp destination chosen from `RainWorld.levelBadWarpTargets`), which in vanilla only fires from specific story-driven triggers.

Rain Meadow already has direct precedent for solving problem #1's shape: `Meadow/RainMeadow.MeadowHooks.cs:254-330` generalizes `RegionGate`'s player-only checks (`PlayersStandingStill`, `PlayersInZone`, `AllPlayersThroughToOtherSide`, `MeetRequirement`) to work for *any* Meadow avatar creature by looking up `mgm.avatars[i]` and its `CreatureController` (via `Meadow/Creatures/CreatureController.cs`) instead of `Player`. The warp work follows the same template. Rain Meadow also already has a full (but Story-mode-only) warp-replication pipeline in `Story/StoryHooks.cs`, `Story/StoryRPCs.cs`, `Story/StoryHelpers.cs`, and `Game/RainMeadow.EntityHooks.cs` for replaying Watcher rift warps across clients during the story campaign — that pipeline is the template for a Meadow-sandbox equivalent, not something to modify directly (it's gated by `isStoryMode(...)` and must keep working unchanged for story sessions).

Decisions made for this plan (confirmed with the user):
- Progression gating (ripple/Prince/ripple-egg filters) is **disabled outright** in Meadow, mirroring the existing always-true `RegionGate_MeetRequirement`/`RegionGate_EnergyEnoughToOpen` pattern — not by forcing save-state counters to max.
- Corrupted/"failed warp" destinations are **hand-authored fixed rooms** on dedicated warp points (reusing vanilla's red/corrupted `EffectSettings.BadWarpCosmetics()` visual preset), not vanilla's live seeded-RNG `badWarp` destination roll.
- Region access is **not force-opened**. Instead, the entire warp feature (Phases 1-4) is gated on the lobby's selected timeline being Watcher: `OnlineManager.lobby.meadowTimeline == Watcher.WatcherEnums.SlugcatStatsName.Watcher.value` (the idiomatic string-based comparison already used throughout `Online/Resource/Lobby.cs`/`GameModes/OnlineGameMode.cs`, since `meadowTimeline` is a plain `string`, not a `SlugcatStats.Timeline`/`Name` instance). If the lobby wasn't created with Watcher as the timeline, none of the warp hooks activate at all — no filter-disabling, no cross-species triggering, no region transfer. This sidesteps needing to force open every region: Watcher's own `SlugcatStats` region-access list already covers the regions relevant to its campaign (including corrupted/rot-adjacent areas), so `OnlineGameMode.EstablishWorlds` ([GameModes/OnlineGameMode.cs:291-297](GameModes/OnlineGameMode.cs#L291-L297)) needs no override as long as curated warp destinations (Phase 6) stay within regions Watcher's timeline already has access to — verify this holds for whichever specific rooms get curated, rather than assuming it up front.

## Recommended Approach

### Phase 1 — Generalize warp triggering to any avatar creature
Add Meadow-gated hooks on `Watcher.WarpPoint` (same hook style already used in `Story/StoryHooks.cs:125-133`: `On.Watcher.WarpPoint.Update`, `.SuckInCreatures`, `.WarpPrecast`) that, when `OnlineManager.lobby?.gameMode is MeadowGameMode mgm` **and** `OnlineManager.lobby.meadowTimeline == Watcher.WatcherEnums.SlugcatStatsName.Watcher.value`, treat the local avatar creature (`mgm.avatars[i].realizedCreature`, looked up the same way `RegionGate_PlayersStandingStill1`/`RegionGate_PlayersInZone1` do at [Meadow/RainMeadow.MeadowHooks.cs:297-330](Meadow/RainMeadow.MeadowHooks.cs#L297-L330)) as eligible for the `Player`-only activation bookkeeping (`warpPointCooldown`, `standingInWarpPointProtectionTime`, `performingActivationTimer`, `activationTime`). Only the *local* client's own avatar needs to drive this — each client runs its own local simulation, consistent with how region-gate crossing already works per-client in Meadow.

Guard everything behind the `MeadowGameMode` check so Story mode (which already has correct Watcher/Player-only semantics) is untouched.

### Phase 2 — Generalize the region/room transfer
This is the harder half. `OverWorld.WorldLoaded()` only moves `game.Players` (grasped items, `AbstractCreature.InitiateAI()`, stomach contents, guide-killing). For a non-Player avatar to actually land in the new room/region, Meadow needs its own post-hook (alongside the existing hooks in [Game/RainMeadow.EntityHooks.cs:373-432](Game/RainMeadow.EntityHooks.cs#L373-L432), which already wrap `OverWorld.WorldLoaded`/`InitiateSpecialWarp_WarpPoint`) that performs the equivalent relocation for the triggering avatar's `AbstractCreature` when it isn't a `Player`. Same `MeadowGameMode` + `meadowTimeline == Watcher` guard as Phase 1.

The disabled-for-Meadow `WarpPoint_PerformWarp` handler at [Story/StoryHooks.cs:495-539](Story/StoryHooks.cs#L495-L539) is the right starting point structurally — it already knows how to strip non-owned remote entities out of the abstracted room via `OnlinePhysicalObject`/`RoomSession`/`WorldSession` — but it currently returns early via `if (!isStoryMode(out var storyGameMode)) return;`, so none of its bookkeeping runs in Meadow today. Write a parallel Meadow-mode branch (or generalize this method to handle both) that:
- Carries the triggering avatar's `AbstractPhysicalObject` (and anything it's grasping) into the new room/region the way vanilla's `Players` loop does, regardless of species.
- Leaves other clients' avatars/entities alone — each client's own `OverWorld` swap is local, matching how independent region-gate crossings already work today without forcing every client to reload.
- Broadcasts the result via a new RPC modeled on `StoryRPCs.NormalExecuteWatcherRiftWarp`/`EchoExecuteWatcherRiftWarp` + `StoryHelpers.PerformWarpHelper` ([Story/StoryRPCs.cs:248-267](Story/StoryRPCs.cs#L248-L267), [Story/StoryHelpers.cs:40-120](Story/StoryHelpers.cs#L40-L120)), so remote clients keep that `OnlineCreature`'s resource activation/position consistent without their own `OverWorld` swapping.

### Phase 3 — Disable progression gating on warp availability
Patch the placed-object filter path that sets `PlacedObject.deactivatedByWarpFilter` (the classes backing `RippleLevelFilter`/`PrinceFilter`/`RippleEggFilter`/`WarpFilter`) to always report the requirement met when `OnlineManager.lobby?.gameMode is MeadowGameMode` **and** the lobby's timeline is Watcher (same guard as Phase 1), following the exact pattern of `RegionGate_MeetRequirement`/`RegionGate_EnergyEnoughToOpen` at [Meadow/RainMeadow.MeadowHooks.cs:256-272](Meadow/RainMeadow.MeadowHooks.cs#L256-L272). This keeps every placed `WarpPoint` active regardless of ripple level, Prince encounters, or ripple-egg count.

### Phase 4 — Corrupted / "failed warp" destinations
Rather than triggering vanilla's live `ChooseDynamicWarpTarget(badWarp: true)` RNG roll, hand-place dedicated warp points whose `WarpPointData` sets a fixed `destRegion`/`destRoom` pointing at a curated "corrupted" location, while still applying `EffectSettings.BadWarpCosmetics()` for the vanilla red/corrupted visual treatment (heavier vignette/spiral/darkness) so they read visually as failed-warp destinations. This sidesteps needing to sync any per-save RNG seed/counter across clients and gives full control over which corrupted rooms are reachable. Building the curated list itself is a content task, not a code task — flag it as follow-up work once the mechanical pieces above are in place.

### Phase 5 — Gate the feature on the Watcher timeline, not on region access
Rather than overriding `OnlineGameMode.EstablishWorlds` ([GameModes/OnlineGameMode.cs:291-297](GameModes/OnlineGameMode.cs#L291-L297)) to force open every region, require the Meadow lobby to be running with Watcher as its timeline before any warp behavior activates at all: `OnlineManager.lobby.meadowTimeline == Watcher.WatcherEnums.SlugcatStatsName.Watcher.value` (set from `Menu/LobbyCreateMenu.cs`'s timeline dropdown at lobby creation, synced via `Lobby.LobbyState.timeline`). Since `overworldSession.overWorld.regions` is already derived from the selected timeline's `SlugcatStats` access rules, and Watcher's campaign already has broad region access (including rot/corrupted-adjacent areas relevant to failed warps), this should cover the regions needed for curated destinations without touching `EstablishWorlds` at all. This directly addresses the current gap where "warps add some areas, but not vanilla regions, so players can't get to other regions" — by scoping the whole feature to the one timeline where vanilla's region access already lines up with what warping needs, instead of forcing region access open for every timeline.

If, once Phase 6's destination list is curated, some corrupted/curated room turns out to sit in a region Watcher's own `SlugcatStats` access list doesn't include, that's the point to revisit `EstablishWorlds` for that specific case — not to preemptively force every region open.

### Phase 6 — Curate real destination rooms

**Correction (superseding the original framing above):** Watcher regions have no `RegionGate` connections to reuse — confirmed by inspecting the actual game install (`~/.local/share/Steam/steamapps/common/Rain World/RainWorld_Data/StreamingAssets/mods/watcher/world/gates/`), which is **empty**, vs. the base game's `world/gates/` (57 files, one pair per vanilla `RegionGate`). Watcher's own region connectivity is a separate, `WarpPoint`-based graph authored directly on placed objects inside room `_settings.txt` files, unrelated to gates. Phase 6 is therefore not "point at the RegionGate-adjacent room" — it's: enumerate Watcher's regions, enumerate its already-placed `WarpPoint` objects (which already encode a partial region-to-region graph), reuse every link that already exists, and hand-author a fixed `destRegion`/`destRoom` only where the graph currently has a gap.

**1. All regions in Watcher** (from `mods/watcher/world/`, `displayname.txt` per region folder):

30 dedicated Watcher regions: `wara` Shattered Terrace, `warb` Salination, `warc` Fetid Glen, `ward` Cold Storage, `ware` Heat Ducts, `warf` Aether Ridge, `warg` The Surface, `waua` Ancient Urban, `wbla` Badlands, `wdsr` Decaying Tunnels, `wgwr` Infested Wastes, `whir` Corrupted Factories, `wmpa` Migration Path, `wora` Outer Rim, `wpga` Pillar Grove, `wpta` Signal Spires, `wrfa` Coral Caves, `wrfb` Turbulent Pump, `wrra` Rusted Wrecks, `wrsa` Daemon, `wska` Torrential Railways, `wskb` Sunbaked Alley, `wskc` Stormy Coast, `wskd` Shrouded Stacks, `wssr` Unfortunate Evolution, `wsur` Crumbling Fringes, `wtda` Torrid Desert, `wtdb` Desolate Tract, `wvwa` Verdant Waterways, `wvwb` Fractured Gateways.

Plus 5 base-game regions Watcher overlays with extra content but doesn't turn into a distinct region (`cc`, `hi`, `lf`, `sh`, `su`) — none of these contain any placed `WarpPoint` objects (verified below), so they sit outside the warp graph entirely; they're already reachable in a Watcher-timeline lobby through whatever normal means Watcher's `SlugcatStats` access already grants (per Phase 5), so they need no warp-graph work.

**2. All warp point possibilities in Watcher** — found by grepping every `*_settings.txt` under `mods/watcher/world/*-rooms/` for `PlacedObject.Type.WarpPoint` entries and parsing the `~`-delimited `WarpPointData.ToString()` payload (`Watcher/WarpPoint.cs`'s `WarpPointData.FromString`: field 4 = `destRegion`, field 5 = `destRoom`). 84 placed `WarpPoint` objects exist across the Watcher regions (none in `cc`/`hi`/`lf`/`sh`/`su`).

**2b. Echoes are the same mechanism, not a separate one.** The vanilla "how do I get to another Watcher region" prompt is normally an Echo, not a warp point directly — but `Watcher.SpinningTopData.CreateWarpPointData(Room room)` (decompiled from the assembly) builds a `WarpPoint.WarpPointData` straight out of the Echo's own `destRegion`/`destRoom`/`destTimeline` fields and drives an ordinary `WarpPoint` transition under the hood. An Echo *is* a `WarpPoint` with a conversation panel and `spawnIdentifier`/discovery-gating bolted on, not a different transport mechanism — so Phases 1-4's generic `WarpPoint` generalization already covers "what an Echo does" for free, and Meadow doesn't need to reimplement or impersonate the Echo/panel system at all. Grepping every `SpinningTopSpot` in the whole game install (all DLCs, not just the Watcher folder) turns up only 16 total, and only **two cross between a vanilla region and a Watcher region**: `lf_b01w` (Farm Arrays) → `wrfa_sk04` (Coral Caves), and `waua_bath` (Ancient Urban) → `sb_d07` (Subterranean, outbound only). Every other Echo just links two Watcher-only regions together — so in vanilla, Echoes aren't the broad "front door" into the Watcher region cluster; the campaign simply starts the player inside it already. Both parsed tables (kind `WarpPoint` and kind `Echo`, source region/room → dest region/room) are saved together at [plan/watcher-warp-links.tsv](watcher-warp-links.tsv) so neither scan needs to be redone.

**3 & 4. Reuse existing links; hand-author only the gaps:**

Most non-`NULL` `WarpPoint` rows already form reciprocal pairs (e.g. `warb_f01↔ware_h21`, `warc_b12↔warb_f18`) — for every region pair that already has a fixed link in the table (either kind), **reuse it as-is** for the Meadow warp destination; do not invent a new room for a link that already exists. The two vanilla-crossing Echo rows are the natural candidates for Meadow's actual "enter the Watcher region cluster from a normal lobby" warp points, since they're already-authored, already-tested crossing rooms rather than an arbitrary pick: place a Meadow `WarpPoint` in `lf_b01w` (or reuse it directly) targeting `wrfa_sk04`, and one in a room in `sb` targeting `waua_bath` (or, since that Echo only runs outbound, mirror it — put the Meadow entry point's `destRoom` at `waua_bath` from `sb_d07`'s room directly).

Cross-referencing every region name against the table's `sourceRegion`/`destRegion` columns (both kinds combined) turns up **four regions with zero fixed (`non-NULL`) connectivity in either direction**: `wdsr`, `wgwr`, `whir`, `wsur`. (`waua` drops off this list once Echoes are counted — it has a real, if one-directional, outbound link to `sb_d07`.) These four are the remaining gaps needing a hand-authored `destRegion`/`destRoom` on a Meadow-placed `WarpPoint`:

- `wdsr`/`wgwr`/`whir`/`wsur` (Decaying Tunnels, Infested Wastes, Corrupted Factories, Crumbling Fringes) are **rot-mirror overlays** of vanilla `ds`/`gw`/`hi`/`su` — confirmed by matching room-id numbering (e.g. `wdsr_a07`/`a08`/`a11`… mirrors `ds_a07`/`a08`/`a11`…) and each region's `properties.txt` carrying `Room Setting Templates: Rot`. In vanilla they're reached by the *same* region corrupting in place (a runtime asset-swap tied to spreading-rot progression), never by a discrete jump — which is exactly why no `WarpPoint` or Echo targets them. Since Meadow's sandbox has no spreading-rot state to trigger that swap, these four need a genuinely new hand-placed `WarpPoint` (in some other reachable room) whose `destRegion`/`destRoom` targets that rot-region's own entrance room directly.
- `waua` (Ancient Urban, reachable but only outbound via the Echo above) and the one-way-only `wara` (Shattered Terrace — has one *incoming* fixed link from `wrsa_d01→WARA_P17` but no outgoing link back out) are self-contained scripted-sequence areas: `waua`'s rooms carry a distinct `A01`/`A01B`/`BATH`/`TOYS`/`SHOP` naming scheme (a dream/memory sequence, not a normal explorable map) and `wara`'s `properties.txt` is full of `Room_Attr: ...-Forbidden/-Like/-Stay` squad tags (an arena/encounter room). Before wiring either as an ordinary two-way warp destination, verify by hand that the specific room chosen doesn't assume mid-sequence state that a sandbox arrival would break — treat these two as needing individual verification, not the same mechanical treatment as the four rot regions above.

## Critical files
- [GameModes/OnlineGameModeHelpers.cs](GameModes/OnlineGameModeHelpers.cs) — cosmetic/creature/grabbable allowlists (`WarpPoint` already added).
- [GameModes/OnlineGameMode.cs:162-176,213-229,291-297](GameModes/OnlineGameMode.cs#L162-L297) — `FilterItems`/`AllowedInMode`, `LoadWorldAs`/`LoadWorldIn` (consumes `meadowTimeline`), `EstablishWorlds`.
- [Online/Resource/Lobby.cs:35,50-61,278,314,323](Online/Resource/Lobby.cs#L35-L323) — `meadowTimeline`/`ActiveTimeline`, the fields the Phase 1/2/3/5 guard reads.
- [Menu/LobbyCreateMenu.cs:106-137,200,230-233](Menu/LobbyCreateMenu.cs#L106-L233) — where `meadowTimeline` is chosen/set at lobby creation.
- [Meadow/RainMeadow.MeadowHooks.cs:254-330](Meadow/RainMeadow.MeadowHooks.cs#L254-L330) — the `RegionGate` generalization pattern to mirror for Phases 1 and 3.
- [Meadow/Creatures/CreatureController.cs](Meadow/Creatures/CreatureController.cs) — avatar-creature abstraction (`BindAvatar`, `creatureControllers` map) used to look up the local avatar regardless of species.
- [Story/StoryHooks.cs:125-133,226-330,471-539](Story/StoryHooks.cs#L125-L539) — existing `Watcher.WarpPoint` hooks and the disabled `WarpPoint_PerformWarp` handler to generalize/parallel for Meadow mode.
- [Story/StoryRPCs.cs:248-267](Story/StoryRPCs.cs#L248-L267) and [Story/StoryHelpers.cs:40-123](Story/StoryHelpers.cs#L40-L123) — RPC/replay pattern template for Phase 2's cross-client broadcast.
- [Game/RainMeadow.EntityHooks.cs:373-432,508-750](Game/RainMeadow.EntityHooks.cs#L373-L750) — existing `OverWorld.WorldLoaded`/`InitiateSpecialWarp_WarpPoint` hooks to extend in Phase 2.
- Vanilla reference: no decompiled source tree is checked into this repo, but two things are available locally when this plan needs re-verifying — (a) `ikdasm`/`ilspycmd` against `Mod/plugins/PUBLIC-Assembly-CSharp.dll` for code (`Watcher.WarpPoint`'s `SuckInCreatures`, `PerformWarp`, `ChooseDynamicWarpTarget`, `blackListedCreatureTypes`; `Watcher.SpinningTopData.CreateWarpPointData` — confirms Echoes are just `WarpPoint`s under the hood; `OverWorld`'s `WorldLoaded`, `warpData`, `WarpUpdate`; the `RippleLevelFilter`/`PrinceFilter`/`RippleEggFilter`/`WarpFilter` classes), and (b) the actual Steam install at `~/.local/share/Steam/steamapps/common/Rain World/RainWorld_Data/StreamingAssets/` for game *data* — `mods/watcher/world/gates/` (empty — confirms no Watcher `RegionGate`s) and `mods/watcher/world/*-rooms/*_settings.txt` (room-placed-object data, the source for Phase 6's warp graph, extraction method documented there; grepping `SpinningTopSpot` across the *whole* install, not just the watcher folder, is what turned up the two vanilla-crossing Echoes).
- [plan/watcher-warp-links.tsv](watcher-warp-links.tsv) — the parsed Phase 6 connectivity data: every placed `WarpPoint` (84) and `SpinningTopSpot`/Echo (16) with its source room and `destRegion`/`destRoom` (`NULL`/`NULL` if unauthored/dynamic), tagged by `kind`. Re-derive by grepping `mods/watcher/world/*-rooms/*_settings.txt` (whole install for Echoes) for `WarpPoint><...>`/`SpinningTopSpot><...>` and parsing the `~`-split data payload (`WarpPointData`: fields 4/5 = destRegion/destRoom; `SpinningTopData`: fields 3/4) if the mod's own room set changes.

## Verification
Once implemented:
1. Build the mod and load a Meadow lobby created with the timeline dropdown set to Watcher, with a test room containing a placed `WarpPoint` (normal destination), a "corrupted" warp point (fixed bad-warp-flavored destination), and — if reachable in the test setup — a `RippleLevelFilter`/`PrinceFilter`-gated warp point.
2. Join with avatars of at least two non-Player species (e.g. Lizard and Scavenger, via `CreatureController.BindAvatar`) and confirm both can trigger and complete a warp (region swap actually happens, creature and grasped items land in the destination room).
3. Confirm the gated warp point stays active/usable regardless of the local save's ripple level/Prince-encounter count.
4. With two clients connected, have one client warp and verify the other client's view of that avatar (position, resource activation) stays consistent without its own `OverWorld` swapping.
5. Trigger a corrupted warp point and confirm arrival at the curated destination with the red/corrupted visual treatment.
6. Create a second lobby with a non-Watcher timeline and confirm none of the warp behavior activates (filters still gate normally, non-player creatures can't trigger a warp, `Player`-only vanilla behavior is otherwise untouched) — this is the primary regression check for the Phase 5 guard.
7. Confirm every curated destination room (Phase 6) actually resolves under Watcher's normal `SlugcatStats` region access — if any doesn't, that's the signal to revisit `EstablishWorlds` for that region specifically.

## Note for next session
Phase 6 research is done and verified against the actual game install (not guessed) — see the rewritten Phase 6 section and [plan/watcher-warp-links.tsv](watcher-warp-links.tsv) (now covers both `WarpPoint`s and Echoes/`SpinningTopSpot`s). Phases 1-5 are still design-only, nothing has been implemented yet. Also answered: "how do players get into the Watcher regions at all" — Echoes are confirmed to be `WarpPoint`s under the hood (`SpinningTopData.CreateWarpPointData`), so no separate entry mechanism is needed; the two already-authored vanilla-crossing Echo rooms (`lf_b01w`→`wrfa_sk04`, `waua_bath`↔`sb_d07`) are the natural anchor rooms for Meadow's own entry warp point(s), reusing authored geometry instead of picking arbitrary rooms.

Next concrete step is either: (a) start Phase 1 (generalizing warp triggering to any avatar creature), or (b) finish curating Phase 6 by (i) picking which of the two vanilla-crossing Echo rooms — or another reachable room entirely — hosts Meadow's actual "enter Watcher regions" warp point, and (ii) picking real entrance rooms for the four remaining gap regions (`wdsr`, `wgwr`, `whir`, `wsur` — `waua` no longer counts as a full gap since its Echo gives it one real outbound link). Both of those are content decisions, not something to infer from code, so they need the user's input before being written down as fixed `destRegion`/`destRoom` values.

**2026-08-26 update:** Phase 1 has been broken into six subtask files — start at
[plan/phase1-00-overview.md](phase1-00-overview.md). That overview also records two
corrections to this document's Phase 1 framing worth reading before touching Phase 2-6: (1)
the `Player`-only activation bookkeeping (`warpPointCooldown`, `standingInWarpPointProtectionTime`,
`performingActivationTimer`, etc.) turned out to be fields declared directly on the `Player`
class rather than something a type-check widen alone can generalize — it needs a companion
per-creature state store (see [plan/phase1-02-creature-warp-state.md](phase1-02-creature-warp-state.md)).
(2) This ships as a **new standalone mod project** in this repo (modeled on `MeadowMounts/`),
not as edits to the sibling `/home/preston/repos/Rain-Meadow` source tree — this document's
own file-path references (`Meadow/RainMeadow.MeadowHooks.cs`, `Story/StoryHooks.cs`, etc.)
point into that sibling repo and are read-only reference material for the pattern to mirror,
not edit targets. A decompiled copy of `Watcher.WarpPoint` is now persisted at
[plan/reference/Watcher.WarpPoint.decompiled.cs](reference/Watcher.WarpPoint.decompiled.cs) so
it doesn't need re-extracting from the game install.

**2026-08-26 update 2:** Phase 2 has also been broken into three subtask files, mirroring
Phase 1's structure — start at [plan/phase2-00-overview.md](phase2-00-overview.md). Its
investigation found the disabled `WarpPoint_PerformWarp` entity-stripping (Story/StoryHooks.cs
lines 495-539 referenced above) is gated on Story mode specifically, not on species — it's
fully off in every Meadow lobby today, not just for non-`Player` avatars. It also flagged that
Phase 2's third piece (cross-client `OnlineCreature` sync) may not need a hand-written RPC at
all if Meadow's existing region-gate crossing already keeps remote avatar state in sync — that
needs verifying before assuming Story's `NormalExecuteWatcherRiftWarp`/`PerformWarpHelper`
pattern must be duplicated. Do not start Phase 2 implementation before Phase 1 is verified, per
[plan/phase1-00-overview.md](phase1-00-overview.md)'s own note.

**2026-08-26 update 3:** Phase 3 has also been broken into three subtask files — start at
[plan/phase3-00-overview.md](phase3-00-overview.md). Its investigation (decompiling
`RoomSettings.cs`/`PlacedObject.cs`) found this phase's actual mechanism is a one-time bulk
deactivation pass in `RoomSettings.LoadPlacedObjects` (called from the `RoomSettings` constructor
per room load), not a per-object runtime check — and that it's more urgent than "progression
gated": the three real filters (`RippleLevelFilterData`/`PrinceFilterData`/`RippleEggFilterData`)
already return `false` unconditionally outside a story session, meaning every Meadow lobby today
already force-deactivates any placed object near one of them, regardless of save state. It also
found `PlacedObject.Type.WarpFilter` (which the parent bullet above lumped in with the other
three) is a structurally separate, unconditional radius-based deactivation zone with no
progression logic at all — [plan/phase3-03-warpfilter-scope-decision.md](phase3-03-warpfilter-scope-decision.md)
flags that as needing its own user decision rather than the same automatic fix. Phase 3 has no
code dependency on Phase 1/2, but confirmed hookability precedent for `RoomSettings`'s
constructor is already visible in the sibling Rain-Meadow repo (`Game/RainMeadow.GameHooks.cs:49-50`
hooks it both via `On.*` and `IL.*` today) — see phase3-01's file for details. Nothing in Phase 3
is implemented yet.

**2026-08-26 update 4:** Phase 4 (corrupted/failed-warp destinations) has been broken into three
subtask files and is now implemented end-to-end (not yet in-game tested) — start at
[plan/phase4-00-overview.md](phase4-00-overview.md).

Phase 5 (gate the feature on the Watcher timeline) has also been broken into three subtask files
— start at [plan/phase5-00-overview.md](phase5-00-overview.md). Its overview found the parent
framing above was already satisfied piecemeal: every hook file implemented so far (Phases 1/3/4)
already independently gates on `MeadowGameMode` + `meadowTimeline == Watcher`, so Phase 5's
remaining scope is narrower than "write the gate" — a guard-consolidation decision (4 duplicated
copies of the same check), a read-only verification that `meadowTimeline` is populated before any
room can load, and a region-access check against Phase 6's curated destination list once that
list exists (blocked until then).
