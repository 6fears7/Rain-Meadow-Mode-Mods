# Phase 1.2 — Companion per-creature warp state (foundation)

Parent: [phase1-00-overview.md](phase1-00-overview.md). Depends on: [[phase1-01-mod-scaffold]].
Depended on by: [[phase1-03-trigger-detection]], [[phase1-04-suckin-creatures]],
[[phase1-05-changestate-effects]], [[phase1-06-exit-placement]].

## Goal
Give every non-`Player` Meadow avatar creature the same per-creature warp bookkeeping that
vanilla `Player` carries as fields, so the other subtasks have somewhere to read/write it
instead of touching `Player`-typed fields directly.

## Why this exists
Confirmed by grepping the decompile at
[plan/reference/Watcher.WarpPoint.decompiled.cs](reference/Watcher.WarpPoint.decompiled.cs):
`warpPointCooldown`, `warpExhausionTime`, `standingInWarpPointProtectionTime`,
`performingActivationTimer`, `customPlayerGravity`, `cancelCamoCooldown`,
`camoInputsNeedReset` are declared on `Player` itself (search the sibling repo's own
`PUBLIC-Assembly-CSharp.dll` decompile of `Player.cs` if the exact declaring type of any of
these is ever in doubt — the WarpPoint decompile only shows the *usage* sites, e.g. line 2341
`player2.warpPointCooldown`, line 2761 `player.standingInWarpPointProtectionTime`, line 2766
`player.cancelCamoCooldown`). `Creature`/`CreatureController` has none of them. Since the
target assembly is publicized (see [[phase1-01-mod-scaffold]]), the *vanilla* `Player` fields
stay directly writable when the local avatar genuinely is a `Player` — this subtask is only
about giving non-`Player` creatures an equivalent, not about changing how `Player` avatars are
handled (they already work).

## What to build
A `ConditionalWeakTable<Creature, WarpState>` (same idiom as
`CreatureController.creatureControllers` in
[/home/preston/repos/Rain-Meadow/Meadow/Creatures/CreatureController.cs:13](../../Rain-Meadow/Meadow/Creatures/CreatureController.cs#L13))
keyed by the avatar's realized `Creature`, holding a small class/struct with fields mirroring
the vanilla ones this feature actually needs:
- `warpPointCooldown` (int, counts down, vanilla sets it to 80 in several places)
- `warpExhausionTime` (int)
- `standingInWarpPointProtectionTime` (int, vanilla sets to 10)
- `performingActivationTimer` (int)
- `triggeredWarpPoint` (a `WarpPoint`-or-null reference — this is the non-Player stand-in for
  `playerTriggeredWarpPoint`, since that field's `Player` type can never hold a Lizard/Scavenger)

Skip `customPlayerGravity`/`cancelCamoCooldown`/`camoInputsNeedReset` unless a later subtask's
implementation turns out to need them — they're camera/camo-specific polish, not required for
"a warp actually triggers and completes."

Add a small helper (e.g. a static method on this new state-holder type) that returns the
state for a given `Creature`, auto-creating it on first access — mirrors how
`CreatureController.creatureControllers.TryGetValue` is used at
[/home/preston/repos/Rain-Meadow/Meadow/RainMeadow.MeadowHooks.cs:301](../../Rain-Meadow/Meadow/RainMeadow.MeadowHooks.cs#L301).
Also add a small static helper for "is this the local client's own avatar and is it not a
`Player`" — the guard every other subtask needs:
```
OnlineManager.lobby?.gameMode is MeadowGameMode mgm
  && OnlineManager.lobby.meadowTimeline == Watcher.WatcherEnums.SlugcatStatsName.Watcher.value
  && mgm.avatars[0].realizedCreature is Creature c && c is not Player
```
(the `mgm.avatars[0]` idiom for "the local avatar" is used throughout RainMeadow.MeadowHooks.cs
— e.g. lines 288, 301, 317, 437 — confirmed to be the established pattern, not a
Meadow-multi-avatar-per-client special case).

## Done when
- The new state type compiles and is reachable from the new mod project.
- A unit-free manual check: binding a Lizard avatar via `CreatureController.BindAvatar` (no
  code change needed here, just confirms the `ConditionalWeakTable` key type lines up) and
  fetching its warp state twice returns the same instance.

## Note for next session
Done as of 2026-08-26. Implemented at
[WatcherWarps/src/CreatureWarpState.cs](../WatcherWarps/src/CreatureWarpState.cs):
- `CreatureWarpState` class with the five fields listed above
  (`customPlayerGravity`/`cancelCamoCooldown`/`camoInputsNeedReset` skipped as planned).
- `static ConditionalWeakTable<Creature, CreatureWarpState> states` + `Get(Creature)` using
  `states.GetValue(creature, c => new CreatureWarpState())` for the auto-create-on-first-access
  helper (equivalent to the `TryGetValue` idiom cited in the plan, just via the single-call
  `GetValue` overload instead of a manual `TryGetValue`/`Add`).
- `TryGetLocalNonPlayerAvatar(out Creature)` static helper implementing the local-avatar guard
  verbatim from this file's snippet — confirmed against
  `/home/preston/repos/Rain-Meadow/GameModes/OnlineGameMode.cs:215` that `meadowTimeline` is a
  bare `string` (so the `.value` comparison in the guard is correct as written) and against
  `Watcher.WarpPoint.decompiled.cs:11` that `WarpPoint` lives in namespace `Watcher` (matches
  the `using Watcher;` added to the new file).
- `dotnet build -p:InstallToGame=false` from `WatcherWarps/` succeeds, 0 warnings/errors —
  confirms the state type and `Watcher`/`RainMeadow` namespace references resolve against the
  publicized assembly + Rain Meadow reference.

Not done: the "Done when" manual check (bind a Lizard avatar via `CreatureController.BindAvatar`
and confirm `Get()` returns the same instance twice) — that requires actually launching the game
with a live lobby, not just a compile check. No hooks call `Get`/`TryGetLocalNonPlayerAvatar` yet
either; `Warps.Apply` in [WatcherWarpsPlugin.cs](../WatcherWarps/src/WatcherWarpsPlugin.cs) is
still the scaffold no-op. Next: phase1-03 (trigger detection) is the first subtask that actually
calls into this state type from a hook.
