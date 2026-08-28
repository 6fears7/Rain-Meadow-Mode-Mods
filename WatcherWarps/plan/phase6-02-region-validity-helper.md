# Phase 6.2 — Region-validity + redirect helpers

Parent: [[phase6-00-timeline-region-mismatch-spawn-guard]]

Add to `src/Warps.cs` (or a small new static file if `Warps` gets crowded):

## `bool RegionLoadableInCurrentTimeline(string roomOrRegionName)`

```csharp
// Region acronym = text before first '_'; a bare acronym is passed through.
var acr = roomOrRegionName.Contains('_')
    ? roomOrRegionName.Substring(0, roomOrRegionName.IndexOf('_'))
    : roomOrRegionName;

var timeline = OnlineManager.lobby?.meadowTimeline;
if (string.IsNullOrEmpty(timeline)) return true; // no timeline set -> don't interfere

var name = new SlugcatStats.Name(timeline);
var valid = SlugcatStats.getSlugcatStoryRegions(name)
    .Concat(SlugcatStats.getSlugcatOptionalRegions(name));
return valid.Any(r => string.Equals(r, acr, StringComparison.OrdinalIgnoreCase));
```

Confirm in 6.1/6.3 whether `getSlugcatStoryRegions`/`getSlugcatOptionalRegions` are the same
lists `Region.LoadAllRegions` effectively honors; if there's a timeline-specific wrinkle (e.g.
gate regions, `Region.GetProperRegionAcronym`), widen accordingly. When unsure, err toward
"valid" (return true) — a false redirect to Outskirts is worse than the status quo for regions
that would actually have loaded.

## `void RedirectSaveToOutskirts(ProcessManager manager)`

Mirror `MeadowPauseMenu.ToOutskirts` (`Meadow/MeadowPauseMenu.cs:186-193`):

```csharp
int su = RainWorld.roomNameToIndex["SU_C04"]; // == MeadowPauseMenu.suco4
MeadowProgression.progressionData.currentCharacterProgress.saveLocation =
    new WorldCoordinate(su, -1, -1, 0);
manager.menuSetup.regionSelectRoom = "SU_C04"; // == MeadowProgression.defaultStartingRoom
MeadowProgression.SaveProgression(); // persist so it doesn't retrigger next launch
```

Check `MeadowProgression.SaveProgression()` is safe to call here (StartGame already calls it at
`MeadowMenu.cs:326`, so yes for that path).
