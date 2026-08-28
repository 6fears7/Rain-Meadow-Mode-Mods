using System;
using BepInEx.Logging;

namespace WatcherWarps
{
    // Rain Meadow's On.ShortcutGraphics.Draw hook (Rain-Meadow/Meadow/RainMeadow.MeadowHooks.cs
    // ~161-177) runs an MSC-only loop that dereferences `self.room.shortcuts[k]` for every
    // entry in `self.entranceSprites`. During a Watcher region warp the RoomCamera keeps
    // drawing the outgoing room's ShortcutGraphics for a frame or two after its `room` /
    // world has been torn down, so that loop throws an NRE (self.room is null). It's caught
    // by Meadow's error logger and only skips one frame of cosmetic pipe indicators, but it
    // spams LogOutput.log during every warp.
    //
    // This mod subscribes to On.ShortcutGraphics.Draw after Rain Meadow (SoftDependency load
    // order), so our handler is the outermost wrapper and `orig` here chains into Meadow's
    // handler. Skipping the call when the room is mid-teardown keeps Meadow's loop from ever
    // seeing the null. Guard is scoped to Meadow+Watcher lobbies so nothing else changes.
    public static class ShortcutGraphicsGuardHooks
    {
        internal static ManualLogSource? Log;

        public static void Apply()
        {
            On.ShortcutGraphics.Draw += ShortcutGraphics_Draw;
        }

        private static void ShortcutGraphics_Draw(On.ShortcutGraphics.orig_Draw orig, ShortcutGraphics self, float timeStacker, UnityEngine.Vector2 camPos)
        {
            if (Warps.IsMeadowWatcher()
                && (self.room == null || self.room.shortcuts == null || self.entranceSprites == null))
            {
                return;
            }

            try
            {
                orig(self, timeStacker, camPos);
            }
            catch (Exception e) when (Warps.IsMeadowWatcher())
            {
                // Belt-and-suspenders: a stale ShortcutGraphics can still trip Meadow's loop
                // if the room tears down between the guard above and this call. Cosmetic only.
                Log?.LogWarning($"suppressed ShortcutGraphics.Draw exception during warp: {e.Message}");
            }
        }
    }
}
