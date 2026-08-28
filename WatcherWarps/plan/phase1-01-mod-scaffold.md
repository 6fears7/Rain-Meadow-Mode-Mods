# Phase 1.1 — New mod project scaffold

Parent: [phase1-00-overview.md](phase1-00-overview.md).

## Goal
Create the standalone mod project this whole feature lives in, so subtasks 2-6 have somewhere
to put code and can be built/tested incrementally. No warp logic in this subtask — scaffold
only.

## Reference
Copy the shape of [MeadowMounts/](../MeadowMounts/) — it's the existing example of a mod in
this repo that hooks Rain Meadow:
- [MeadowMounts/MeadowMounts.csproj](../MeadowMounts/MeadowMounts.csproj) — references
  `Assembly-CSharp` (publicized), `Assembly-CSharp-firstpass`, `HOOKS-Assembly-CSharp`,
  `BepInEx`, `0Harmony`, `MonoMod.RuntimeDetour`, `MonoMod.Utils`, `UnityEngine*`, and
  `Rain Meadow` (via `RainMeadowDir` pointing at `../../Rain-Meadow/Mod/plugins`). Has a
  `CopyToMod`/`InstallToGame` MSBuild target pair that copies the built DLL into
  `Mod/plugins/` and then into the live Steam mod folder.
- [MeadowMounts/src/MountsPlugin.cs](../MeadowMounts/src/MountsPlugin.cs) — `BepInPlugin` with
  a soft dependency on `henpemaz.rainmeadow`, hooks `On.RainWorld.OnModsInit`, checks
  `BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("henpemaz.rainmeadow")` before doing
  anything, then calls a static `Apply(Logger)` entry point.
- [MeadowMounts/Mod/modinfo.json](../MeadowMounts/Mod/modinfo.json) and
  [MeadowMounts/Mod/rainmeadow.json](../MeadowMounts/Mod/rainmeadow.json) — mod metadata files
  that ride along in the `Mod/` folder (read these two files verbatim before writing the new
  ones; don't guess the schema).

## What to create
A new top-level directory (suggested name: `WatcherWarps/` — confirm naming with the user if
it matters to them, not a technical decision) with:
- `WatcherWarps.csproj` — same reference set as MeadowMounts's, `AssemblyName`/`RootNamespace`
  set to the new mod's name, same `CopyToMod`/`InstallToGame` targets with paths adjusted.
- `src/WatcherWarpsPlugin.cs` — `BepInPlugin` entry point mirroring `MountsPlugin.cs`'s
  `OnEnable`/`RainWorld_OnModsInit`/soft-dependency-check/try-catch shape. The `Apply(...)`
  call this delegates to doesn't need to do anything yet (subtask 2 fills it in) — a no-op or
  a single log line is fine to prove the scaffold builds and loads.
- `Mod/modinfo.json` and `Mod/rainmeadow.json` — new mod id/name, `henpemaz.rainmeadow` as a
  dependency (check whether MeadowMounts declares it as hard or soft in `rainmeadow.json`
  specifically, since that file's dependency semantics may differ from the C# soft-dependency
  check).

## Done when
- `dotnet build` succeeds against a local `RainWorldDir` (see the `RainWorldDir`/`RainMeadowDir`
  MSBuild properties — override on the command line per the comment in MeadowMounts.csproj).
- The built DLL lands in `WatcherWarps/Mod/plugins/` and, if `InstallToGame=true`, in the
  Steam mod folder alongside `modinfo.json`/`rainmeadow.json`.
- Launching the game with the mod enabled in the in-game mod list produces the scaffold's log
  line and no BepInEx errors (confirms the soft-dependency check and plugin load path work
  before any real logic is added).

## Note for next session
Scaffold created at `WatcherWarps/` (WatcherWarps.csproj, src/WatcherWarpsPlugin.cs,
src/Warps.cs stub, Mod/modinfo.json, Mod/rainmeadow.json), mirroring MeadowMounts. `dotnet
build -p:InstallToGame=false` succeeds and the DLL lands in `WatcherWarps/Mod/plugins/`.
Not yet verified: `InstallToGame=true` copy path (no local `RainWorldDir` override tested
here) and actually launching the game with the mod enabled — that still needs to happen
before this subtask is fully "done" per the Done-when checklist. Everything else in phase1-01
is complete; phase1-02 can start.
