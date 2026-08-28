using System;
using System.Linq;
using HarmonyLib;
using RainMeadow;

namespace WatcherWarps
{
    // Phase 6 (plan/phase6-00-timeline-region-mismatch-spawn-guard.md).
    //
    // meadow.json is a single file shared across every lobby and timeline. It persists each
    // character's last position in
    // MeadowProgression.progressionData.currentCharacterProgress.saveLocation (a
    // WorldCoordinate), written on quit by MeadowGameMode. On "start game",
    // MeadowMenu.StartGame turns that coordinate into
    // manager.menuSetup.regionSelectRoom and the session loads into that room.
    //
    // Save your spot in a Watcher-only region (WARA, WARB, WARC, WAUA, WBLA, WDSR, WORA...),
    // quit, then create/join a lobby on a NON-Watcher timeline and press start: the game
    // tries to load a region that is not in that timeline's region order and NREs / crashes
    // during world load. The fallback guard at MeadowMenu.cs:337-349 is dead code (line 349
    // unconditionally re-assigns the raw saved room name), and RainWorld.roomNameToIndex is
    // built from every region present on disk regardless of timeline, so the name resolves
    // fine and nothing upstream catches the mismatch.
    //
    // This postfix runs after StartGame has assigned the final regionSelectRoom. If that
    // room's region is not in the current timeline's story-or-optional region set, it
    // rewrites BOTH the transient regionSelectRoom and the persisted saveLocation to
    // Outskirts (SU_C04) - mirroring MeadowPauseMenu.ToOutskirts - and re-saves meadow.json
    // so the stale coordinate does not retrigger on the next launch, and so
    // MeadowGameMode.SpawnAvatar (which swaps location back to saveLocation when the rooms
    // match) does not undo the redirect.
    //
    // Unlike every other hook in this mod this MUST NOT gate on Warps.IsMeadowWatcher() -
    // the broken case is precisely a non-Watcher timeline. It gates on
    // "OnlineManager.lobby.gameMode is MeadowGameMode" only, which also catches unrelated
    // mismatches for free (e.g. an MSC-only region saved under a vanilla timeline).
    public static class TimelineRegionGuardHooks
    {
        private const string OutskirtsRoom = "SU_C04"; // == MeadowProgression.defaultStartingRoom

        public static void Apply()
        {
            var target = AccessTools.Method(typeof(MeadowMenu), "StartGame");
            if (target == null) throw new MissingMethodException("MeadowMenu.StartGame not found - Rain Meadow version mismatch?");

            new Harmony(WatcherWarpsPlugin.ModId).Patch(
                target,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(TimelineRegionGuardHooks), nameof(StartGamePostfix))));
        }

        private static void StartGamePostfix(MeadowMenu __instance)
        {
            if (OnlineManager.lobby?.gameMode is not MeadowGameMode) return;

            var manager = __instance.manager;
            string? room = manager?.menuSetup?.regionSelectRoom;
            if (string.IsNullOrEmpty(room)) return;
            if (RegionLoadableInCurrentTimeline(room!)) return;

            Warps.Log?.LogWarning(
                $"Watcher Warps: saved spawn '{room}' is not in timeline "
                + $"'{OnlineManager.lobby.meadowTimeline}' region set; redirecting to Outskirts ({OutskirtsRoom})");

            RedirectSaveToOutskirts(manager!);
        }

        // Region acronym = text before the first '_' (vanilla convention); a bare acronym is
        // passed through. True when the acronym is in the story-or-optional region set for the
        // lobby's current timeline - the union that Region.LoadAllRegions can bring up. When
        // the timeline is unset, or either list can't be resolved, err toward "loadable"
        // (return true): a false redirect to Outskirts is worse than the status quo for a
        // region that would actually have loaded.
        public static bool RegionLoadableInCurrentTimeline(string roomOrRegionName)
        {
            int us = roomOrRegionName.IndexOf('_');
            string acr = us >= 0 ? roomOrRegionName.Substring(0, us) : roomOrRegionName;

            string? timeline = OnlineManager.lobby?.meadowTimeline;
            if (string.IsNullOrEmpty(timeline)) return true;

            SlugcatStats.Name name;
            try { name = new SlugcatStats.Name(timeline); }
            catch { return true; }

            var story = SlugcatStats.SlugcatStoryRegions(name) ?? Enumerable.Empty<string>();
            var optional = SlugcatStats.SlugcatOptionalRegions(name) ?? Enumerable.Empty<string>();

            return story.Concat(optional)
                .Any(r => string.Equals(r, acr, StringComparison.OrdinalIgnoreCase));
        }

        // Mirrors MeadowPauseMenu.ToOutskirts (Meadow/MeadowPauseMenu.cs:186-193). saveLocation
        // and SaveProgression() are internal to Rain Meadow (this mod links the non-publicised
        // "Rain Meadow.dll"), so both are reached reflectively.
        private static void RedirectSaveToOutskirts(ProcessManager manager)
        {
            if (!RainWorld.roomNameToIndex.TryGetValue(OutskirtsRoom, out int su))
            {
                Warps.Log?.LogError(
                    $"Watcher Warps: {OutskirtsRoom} missing from roomNameToIndex; cannot redirect, load-in may still crash");
                return;
            }

            var newCoord = new WorldCoordinate(su, -1, -1, 0);

            try
            {
                object charProgress = MeadowProgression.progressionData.currentCharacterProgress;
                AccessTools.Field(charProgress.GetType(), "saveLocation").SetValue(charProgress, newCoord);
            }
            catch (Exception e)
            {
                Warps.Log?.LogError($"Watcher Warps: failed to rewrite saveLocation: {e}");
            }

            manager.menuSetup.regionSelectRoom = OutskirtsRoom;

            // StartGame already called SaveProgression() (MeadowMenu.cs:326) before this
            // postfix; call it again so the rewritten coordinate is what lands on disk.
            try
            {
                AccessTools.Method(typeof(MeadowProgression), "SaveProgression", Type.EmptyTypes)
                    .Invoke(null, null);
            }
            catch (Exception e)
            {
                Warps.Log?.LogWarning(
                    $"Watcher Warps: SaveProgression() after redirect failed; this launch is safe but the "
                    + $"stale coordinate may persist: {e}");
            }
        }
    }
}
