using System;
using BepInEx.Logging;

namespace WatcherWarps
{
    // Hard-hang fix. Playtest 2026-09-01, BepInEx/NootFlyCrash.log: warping SU_C04 ->
    // WARA/wara_p08 froze the game with ~19,000 identical stack traces and no other output
    // after them.
    //
    // Rain Meadow's MeadowHud.PlayerOnScreenTracker.Update (Rain-Meadow/Meadow/MeadowHud.cs
    // 144-160) has an "avatar is in a neighbouring room" branch:
    //
    //     var connections = camera.room.abstractRoom.connections;
    //     if (avatar.apo.pos.room == connections[i])
    //         var shortcutpos = camera.room.LocalCoordinateOfNode(i);   // :154
    //
    // Room.LocalCoordinateOfNode -> Room.ShortcutLeadingToNode iterates `room.shortcuts`,
    // which only exists once Room.Loaded() has run. A room still sitting in
    // `world.loadingRooms` already has its abstract nodes (so LocalCoordinateOfNode's
    // `node < abstractRoom.nodes.Length` guard passes) but a null `shortcuts`, so it NREs.
    //
    // A region warp puts the camera on the destination room while it is still loading, and
    // the warped avatar's `apo.pos.room` is still an index into the *source* world - a bare
    // int that can collide with one of the destination room's connection indices. That
    // collision is what the crash log caught.
    //
    // The NRE by itself would be survivable; the deadlock is where it is thrown from.
    // RainWorldGame.Update runs `cameras[j].Update()` (-> hud.Update()) *before*
    // `world.loadingRooms[..].Update()` and `overWorld.Update()`. So the throw aborts the
    // frame before the room can make loading progress: `shortcuts` stays null, the next
    // frame throws in the same place, and nothing downstream of the camera loop ever runs
    // again. The warp never completes and the process has to be killed.
    //
    // Fix: skip the HUD update entirely while a live camera points at a room that has not
    // finished loading. One or two skipped HUD frames per warp is invisible, and it lets
    // RainWorldGame.Update reach the loading pump that clears the condition. Scoped to
    // Meadow+Watcher lobbies, same as ShortcutGraphicsGuardHooks.
    public static class MeadowHudGuardHooks
    {
        internal static ManualLogSource? Log;

        // The guard fires every frame it is needed; log the transitions, not the frames.
        private static bool wasSuppressing;
        private static int suppressedFrames;

        public static void Apply()
        {
            On.HUD.HUD.Update += HUD_Update;
        }

        private static void HUD_Update(On.HUD.HUD.orig_Update orig, HUD.HUD self)
        {
            if (Warps.IsMeadowWatcher() && AnyCameraRoomStillLoading(self))
            {
                if (!wasSuppressing)
                {
                    wasSuppressing = true;
                    suppressedFrames = 0;
                    Log?.LogInfo("Watcher Warps: HUD update suppressed - camera room is mid-load "
                        + "(avoids the MeadowHud.PlayerOnScreenTracker NRE deadlock)");
                }
                suppressedFrames++;
                return;
            }

            if (wasSuppressing)
            {
                wasSuppressing = false;
                Log?.LogInfo($"Watcher Warps: HUD update resumed after {suppressedFrames} suppressed frame(s)");
            }

            try
            {
                orig(self);
            }
            catch (Exception e) when (Warps.IsMeadowWatcher())
            {
                // Belt-and-suspenders. A HUD part that throws here would abort
                // RainWorldGame.Update before the loading-room pump, which is precisely the
                // deadlock this file exists to prevent - so swallow it rather than let it
                // reach the camera loop.
                Log?.LogWarning($"Watcher Warps: suppressed HUD.Update exception: {e}");
            }
        }

        // `shortcuts` is the field that actually NREs, but `fullyLoaded` is the flag the game
        // itself uses for "this room is safe to read geometry from", and it is false for a
        // strictly wider window. During normal play the camera holds the previous, fully
        // loaded room until RoomCamera.ChangeRoom swaps it, so this is false only across a
        // warp's world rebuild.
        private static bool AnyCameraRoomStillLoading(HUD.HUD self)
        {
            if (self.rainWorld?.processManager?.currentMainLoop is not RainWorldGame game) return false;
            var cameras = game.cameras;
            if (cameras == null) return false;

            for (int i = 0; i < cameras.Length; i++)
            {
                var room = cameras[i]?.room;
                if (room == null) continue;
                if (room.shortcuts == null || room.Tiles == null || !room.fullyLoaded) return true;
            }
            return false;
        }
    }
}
