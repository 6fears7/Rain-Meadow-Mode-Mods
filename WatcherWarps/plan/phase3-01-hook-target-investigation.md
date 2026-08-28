# Phase 3.1 — Hook target investigation

Parent: [phase3-00-overview.md](phase3-00-overview.md). Foundation subtask — do first; subtasks
2 and 3 both write hook code whose shape depends on the answer here.

## Question

Can the filter logic be intercepted via a HookGen `On.*` stub, or does it need a manual
`MonoMod.RuntimeDetour.Hook` against a reflected `MethodInfo`? And is `roomSettings.game`
guaranteed to be the live per-session `RainWorldGame` (so `OnlineManager.lobby` reads inside the
hook are valid) at the moment the filter logic runs?

## What's already confirmed (no further digging needed on these two points)

1. **`RoomSettings`'s own constructor already has a HookGen stub, and Rain Meadow already hooks
   it.** `On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame` exists and is
   used at `/home/preston/repos/Rain-Meadow/Game/RainMeadow.GameHooks.cs:49` (an `On.*` postfix)
   **and** `:50` (`IL.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame`, an
   IL hook that cursors to just after the `orig(self)`-equivalent `RoomSettings.Reset()` call —
   see lines 236-253 — to inject arena-mode-specific room-file loading). This is direct proof
   the constructor that calls `LoadPlacedObjects` (line 1433 inside it, per
   [[phase3-00-overview]]) is safely hookable both ways, and Rain Meadow's own code already reads
   Meadow lobby state from inside a hook on this exact constructor family. **Read that existing
   hook as the style template** before writing this phase's hook — same file, same constructor
   family, already proven safe to extend.
2. **`roomSettings.game` is live at filter-evaluation time.** The constructor signature itself
   takes `RainWorldGame game` as a parameter (`RoomSettings.cs:1012`) and `LoadPlacedObjects`
   runs synchronously inside that same constructor call, after whatever sets `this.game` (check
   the constructor body directly to confirm `game` field assignment happens before line 1433 —
   expected but worth a two-minute grep-and-confirm, not a reason to redo the whole
   investigation). Since a `RainWorldGame` only exists once a session (Meadow lobby or story
   game) is already running, `OnlineManager.lobby` is guaranteed non-null and correctly populated
   by the time this runs — no "hook fires before lobby exists" race to worry about.

## What's still open

1. **HookGen coverage for the nested filter classes specifically.** `On.RoomSettings.ctor_...`
   existing doesn't automatically mean `On.PlacedObject.RippleLevelFilterData.Active` (or
   similar for `PrinceFilterData`/`RippleEggFilterData`) also has a generated stub — HookGen's
   handling of deeply-nested non-generic classes should be checked directly: does the project's
   `MMHOOK_*.dll` (or live `On.*` namespace via IDE autocomplete/`ilspycmd` against the MMHOOK
   assembly if one exists in this repo's build output) expose `On.PlacedObject.RippleLevelFilterData`
   at all? If not by that route, check by class hierarchy — HookGen stubs are typically generated
   per-declaring-type, so also check whether `On.PlacedObject.GenericFilterData.Active` exists
   (hooking the base virtual once might be enough, since C# virtual dispatch means overriding at
   the base wouldn't work the normal way — a HookGen `On.*` hook on a virtual method intercepts
   calls to that specific override, not all overrides via the base, so each of the three
   concrete filter classes likely needs its own hook regardless).
2. **Fallback: manual `RuntimeDetour.Hook` via reflection**, already precedented in the same file
   Rain Meadow uses for other hard-to-reach members (`new Hook(typeof(RainWorldGame).GetProperty("GamePaused").GetGetMethod(),
   ...)` at `RainMeadow.GameHooks.cs:67`). If HookGen doesn't cover the nested `Active()`
   overrides, this is the fallback: `new Hook(typeof(PlacedObject).GetNestedType("RippleLevelFilterData").GetMethod("Active"), ...)`
   (repeated per filter class), which works regardless of HookGen stub generation since it hooks
   any resolvable `MethodInfo` directly. This mod's own `.csproj` (modeled on `MeadowMounts/`
   per [[phase1-00-overview]]) already needs a `MonoMod.RuntimeDetour` reference either way, since
   Phase 1 doesn't use it but this phase likely will — confirm the reference is present or add it.
3. **Simplest alternative worth ruling in/out before committing to per-filter-class hooks:** since
   all three filter classes' `Active()` bodies are gated on the *same* `!IsStorySession` check
   before diverging, a single IL hook on `RoomSettings.LoadPlacedObjects` itself (patching the
   `!genericFilterData.Active(this, timelinePoint)` condition at the call site, e.g. by also
   checking a Meadow guard right there) might be less code than three separate per-class hooks,
   at the cost of an IL hook (more fragile across game updates than an `On.*` prefix/postfix) vs.
   three simpler method-level hooks. Decide which tradeoff to take before writing
   [[phase3-02-progression-filter-guard]]'s code — this is a judgment call for whoever picks up
   that subtask, not something to pre-decide here.

## Deliverable

A short note (append to this file's own "Note for next session" once done) stating: which hook
style was chosen (HookGen `On.*` per filter class / manual `RuntimeDetour.Hook` per filter class
/ single `IL.RoomSettings.LoadPlacedObjects` cursor hook), and the confirmed field name/timing
for `roomSettings.game`. That decision is what [[phase3-02-progression-filter-guard]] codes
against.

## Note for next session

Not yet investigated beyond what's captured above (the two "already confirmed" points came from
decompiling `RoomSettings.cs`/`PlacedObject.cs` and grepping the sibling Rain-Meadow repo for
existing precedent — see [[phase3-00-overview]]). The three "still open" items need someone to
actually check the MMHOOK assembly / HookGen output for this project before writing hook code.

## Investigation complete (2026-08-26)

Both open questions resolved by direct inspection — decision made, no further digging needed.

**HookGen coverage confirmed — use per-filter-class `On.*` hooks, no manual `RuntimeDetour.Hook`
needed.** Rain World's HookGen output assembly is `BepInEx/plugins/HOOKS-Assembly-CSharp.dll`
(installed game copy, at `/home/preston/.local/share/Steam/steamapps/common/Rain
World/BepInEx/plugins/HOOKS-Assembly-CSharp.dll` — note this project uses the game's own
`HOOKS-Assembly-CSharp.dll` naming convention, not RoR2-style `MMHOOK_*.dll`, so don't search for
that filename pattern again). Ran `ilspycmd -l c` against it and found individually-generated
nested classes for every filter type: `On.PlacedObject+GenericFilterData`,
`On.PlacedObject+FilterData`, `On.PlacedObject+CompetitiveFilterData`,
`On.PlacedObject+RippleLevelFilterData`, `On.PlacedObject+PrinceFilterData`,
`On.PlacedObject+RippleEggFilterData`. Decompiling `On.PlacedObject+RippleLevelFilterData`
directly confirms each has its own typed `Active` hook event, e.g.:
```
public delegate bool orig_Active(RippleLevelFilterData self, RoomSettings roomSettings, Timeline timelinePoint);
public delegate bool hook_Active(orig_Active orig, RippleLevelFilterData self, RoomSettings roomSettings, Timeline timelinePoint);
public static event hook_Active Active
```
Same shape confirmed for `PrinceFilterData` and `RippleEggFilterData` (each event's delegate is
typed to that concrete class, not shared through the base — matches the overview's prediction
that per-override dispatch means each concrete class needs its own hook). **Decision: hook
`On.PlacedObject.RippleLevelFilterData.Active`, `On.PlacedObject.PrinceFilterData.Active`, and
`On.PlacedObject.RippleEggFilterData.Active` individually as HookGen `On.*` prefix/postfix hooks.**
No `MonoMod.RuntimeDetour` reference needed for this phase after all — drop that from
[[phase3-00-overview]]'s mod-structure assumption unless something else in the mod needs it.

**`roomSettings.game` timing confirmed safe.** Decompiled the `RoomSettings(Room room, string
name, Region region, bool template, bool firstTemplate, SlugcatStats.Timeline timelinePoint,
RainWorldGame game)` constructor from `BepInEx/utils/PUBLIC-Assembly-CSharp.dll`: `this.game =
game;` is the constructor's first statement (line 1015, right after the signature at line 1012),
and `LoadPlacedObjects` isn't called until line 1433 — hundreds of lines later, same constructor
call. `game` is fully assigned long before any filter's `Active()` runs, so `OnlineManager.lobby`
reads inside the hook are safe with no ordering hazard.

**Deliverable for [[phase3-02-progression-filter-guard]]:** write three `On.*` postfix (or
prefix, implementer's choice) hooks — one each on `RippleLevelFilterData.Active`,
`PrinceFilterData.Active`, `RippleEggFilterData.Active` — each checking the Meadow+Watcher guard
from [[phase1-00-overview]] (`OnlineManager.lobby?.gameMode is MeadowGameMode &&
OnlineManager.lobby.meadowTimeline == Watcher.WatcherEnums.SlugcatStatsName.Watcher.value`) and
returning `true` when it matches, falling back to `orig(self, roomSettings, timelinePoint)`
otherwise. No IL hook, no reflection-based `RuntimeDetour.Hook`, no single combined hook on
`RoomSettings.LoadPlacedObjects` — three independent, low-risk `On.*` hooks is both the simplest
and most robust-across-updates option now that per-class coverage is confirmed, so the
"simplest alternative" tradeoff from item 3 above is resolved in favor of the per-class approach.
