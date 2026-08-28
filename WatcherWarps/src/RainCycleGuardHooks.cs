using System.Collections.Generic;

namespace WatcherWarps
{
    // phase 7-07 Task B: belt-and-braces guard so a clobber can't re-close shelter doors.
    //
    // Task A (WorldLoadedHooks) mirrors RainMeadow's LoadFirstWorld fixup at the right
    // moment on the warp path. This makes the invariant CONTINUOUSLY true: Meadow froze
    // AllowRainCounterToTick (RainMeadow.MeadowHooks.cs:400 => false), so a sub-430
    // rainCycle.timer in a Meadow-Watcher lobby is permanent and pins every ShelterDoor
    // in that world to closedFac = 1 (ShelterDoor.Update re-asserts it every frame while
    // timer < initialWait + openUpTicks = 80 + 350). Repair the timer, then let vanilla's
    // own closeSpeed = -1f animation open the door - do NOT touch closedFac/closeSpeed on
    // ShelterDoor directly (that skips the `num > 0 && closedFac == 0` AImapper-construct
    // transition at ShelterDoor.Update line 1760).
    //
    // If this fires at all, Task A's placement or timing was wrong - B is insurance.
    public static class RainCycleGuardHooks
    {
        private const int MeadowCycleStartTicks = 430;
        private const int MeadowCycleTimer = 800;

        // Log at most twice per world name: once on first repair, once more the first time
        // we see a repeat (which means a remote owner is clobbering us every network frame).
        private static readonly Dictionary<string, int> repairLogCount = new Dictionary<string, int>();

        public static void Apply()
        {
            // World has no On.World.Update hook target; RainWorldGame.Update post-orig,
            // operating on self.world, is the plan's documented fallback.
            On.RainWorldGame.Update += RainWorldGame_Update;
        }

        private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
        {
            orig(self);
            if (!Warps.IsMeadowWatcher()) return;

            var world = self.world;
            if (world == null) return;
            var cycle = world.rainCycle;
            if (cycle == null || cycle.timer >= MeadowCycleStartTicks) return;

            cycle.timer = MeadowCycleTimer;

            string key = world.name ?? "?";
            repairLogCount.TryGetValue(key, out int count);
            if (count == 0)
            {
                Warps.Log?.LogInfo(
                    $"Watcher Warps: rain-cycle guard - {key} rainCycle.timer was below {MeadowCycleStartTicks} " +
                    $"(shelter doors pinned shut); repaired to {MeadowCycleTimer}. Task A should have handled this pre-realize.");
                repairLogCount[key] = 1;
            }
            else if (count == 1)
            {
                Warps.Log?.LogWarning(
                    $"Watcher Warps: rain-cycle guard - {key} rainCycle.timer went sub-{MeadowCycleStartTicks} AGAIN; " +
                    "a remote WorldSession owner is likely clobbering it every network frame. Silencing further logs for this world.");
                repairLogCount[key] = 2;
            }
        }
    }
}
