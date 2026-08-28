# Phase 1 — Generalize warp triggering to any avatar creature — Overview

Parent plan: [watcher-warping-support.md](watcher-warping-support.md) (Phase 1 section, lines 22-25).

## Correction to the parent plan's framing

The parent plan describes Phase 1 as "add Meadow-gated hooks ... that treat the local avatar
creature as eligible for the `Player`-only activation bookkeeping." Reading the actual
decompiled `Watcher.WarpPoint` (persisted at
[plan/reference/Watcher.WarpPoint.decompiled.cs](reference/Watcher.WarpPoint.decompiled.cs),
~3900 lines, pulled from `PUBLIC-Assembly-CSharp.dll` via `ikdasm`/ilspy — see the parent
plan's "Vanilla reference" bullet under Critical files) shows this is bigger than a type-check
widen:

1. **The bookkeeping fields live on `Player`, not on `Creature`.** `warpPointCooldown`,
   `warpExhausionTime`, `standingInWarpPointProtectionTime`, `performingActivationTimer`,
   `customPlayerGravity`, `cancelCamoCooldown`, `camoInputsNeedReset` are all fields declared
   directly on the `Player` class (confirmed by grepping the decompile — e.g. line 2341
   `player2.warpPointCooldown`, line 2761 `player.standingInWarpPointProtectionTime`). A
   `Lizard`/`Scavenger`/etc. avatar has no such fields at all — there is nothing to "make
   eligible," a parallel store has to be created. [[phase1-01-creature-warp-state]] is that
   store.
2. **Vanilla's whole state machine is driven by `room.game.Players`**, the vanilla
   human-player list (`RainWorldGame.Players`), not by anything Meadow's own avatar tracking
   populates. Non-Player Meadow avatars are never in that list, so today the entire
   `ReadyForWarp → EnterWarp → ExitWarp → SpawnItems → CoolDown` cycle simply never starts for
   them — it's not that they're "not eligible," it's that vanilla's loops never see them. This
   touches four separate methods, not one:
   - `Update()` — trigger detection/selection (`ReadyForWarp` branch) and per-state ticking.
   - `SuckInCreatures()` — the pull/gravity/cooldown-reset effects applied while `EnterWarp` is
     active.
   - `ChangeState()` — side effects fired on entering/leaving each state (cutscene camera,
     `warpPointCooldown` resets, `StopLevitation`).
   - `AddPlayerBackIn()` — placement/levitation on arrival (bad-warp and warp-defer paths).
3. **`playerTriggeredWarpPoint` is typed `Player`** (decompile line 805) and is read in
   `ChangeState`'s `EnterWarp` branch (line 2629:
   `EnterCutsceneMode(playerTriggeredWarpPoint.abstractCreature, ...)`) unconditionally once
   that state is entered. A non-Player creature can never be assigned into a `Player`-typed
   field, so if Meadow forces the state machine into `EnterWarp` for a Lizard trigger without
   also preventing that line from running, it's a guaranteed `NullReferenceException`. This is
   the central gotcha for [[phase1-05-changestate-effects]].
4. **`PerformWarp()` (decompile line 1482) is already species-agnostic** — it only touches
   room/save-state fields, no `Player` casts. Good news: it needs no changes.

## Mod structure decision (confirmed with user)

This ships as a **new standalone mod project in this repo**, modeled on
[MeadowMounts/](../MeadowMounts/) — its own `.csproj` with a `HintPath` to `Rain Meadow.dll`
plus the publicized `PUBLIC-Assembly-CSharp.dll`, its own `BepInPlugin`/`On.*` hooks — not
edits to the sibling `/home/preston/repos/Rain-Meadow` source tree. The parent plan's file
paths (`Meadow/RainMeadow.MeadowHooks.cs`, `Story/StoryHooks.cs`, etc.) are **read-only
reference material** in that sibling repo to study the pattern and confirm nothing there
conflicts — not edit targets. Rain Meadow's own `Story/StoryHooks.cs` hooks on `WarpPoint` are
guarded by `isStoryMode(...)` (confirmed at
`/home/preston/repos/Rain-Meadow/Story/StoryHooks.cs:228`), so they no-op when a Meadow lobby
is active and it's safe for a separate mod's `On.*` hooks to run alongside them — MonoMod
`On.*` hooks chain rather than conflict.

Because the game assembly is publicized (see
[MeadowMounts.csproj](../MeadowMounts/MeadowMounts.csproj)'s comment on
`PUBLIC-Assembly-CSharp.dll`), every field mentioned above — regardless of original
`private`/`internal` access — is directly readable/writable from hook code without reflection.

## Subtask sequence

1. [[phase1-01-mod-scaffold]] — new project skeleton (do first; everything else needs it to compile against).
2. [[phase1-02-creature-warp-state]] — companion per-creature warp state (foundation; every other subtask reads/writes it).
3. [[phase1-03-trigger-detection]] — `Update()`'s `ReadyForWarp` trigger-selection loop.
4. [[phase1-04-suckin-creatures]] — `SuckInCreatures()`'s `Player`-only pull/gravity effects.
5. [[phase1-05-changestate-effects]] — `ChangeState()`'s `EnterWarp`/`ExitWarp` side effects (cutscene, cooldowns, `playerTriggeredWarpPoint` NRE risk).
6. [[phase1-06-exit-placement]] — `ExitWarp`/`CoolDown` state-body placement + `AddPlayerBackIn` (flagged possible overlap with parent-plan Phase 2).

Subtasks 3-6 each depend on subtask 2 but are otherwise independently workable/reviewable —
pick any one without re-reading the others; each subtask file is self-contained with its own
line references into [plan/reference/Watcher.WarpPoint.decompiled.cs](reference/Watcher.WarpPoint.decompiled.cs).

## Note for next session

Nothing implemented yet as of 2026-08-26 — this overview and its six subtask files are the
full Phase 1 breakdown. Start with phase1-01 (scaffold), then phase1-02 (state foundation);
after that, 3-6 can be done in any order. Do not start Phase 2 (region/room transfer,
`OverWorld.WorldLoaded`) until Phase 1 is verified — Phase 2 depends on a triggering avatar
that Phase 1 doesn't yet produce for non-Player species.
