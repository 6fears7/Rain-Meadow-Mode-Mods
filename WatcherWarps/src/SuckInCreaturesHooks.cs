using RainMeadow;
using UnityEngine;
using Watcher;

namespace WatcherWarps
{
    // Generalizes Watcher.WarpPoint.SuckInCreatures' Player-only block (decompile
    // lines 2743-2769) to the local Meadow avatar when it isn't a Player.
    //
    // Gravity-zeroing/velocity-damping and the blacklist/stun/orbit-pull are already
    // species-agnostic in `orig` (plan/reference/Watcher.WarpPoint.decompiled.cs:2775-2794 -
    // the generic physicalObject path there zeroes gravity and applies OrbitVector to
    // any Creature, non-Player included, once it's within range), so this hook only
    // needs to replicate the two things vanilla stores on Player itself:
    // warpPointCooldown and standingInWarpPointProtectionTime. performingActivationTimer's
    // reset (lines 2763-2768) is Watcher-camo bookkeeping only, which phase1-02
    // deliberately left off CreatureWarpState - skipped here for the same reason.
    public static class SuckInCreaturesHooks
    {
        public static void Apply()
        {
            On.Watcher.WarpPoint.SuckInCreatures += WarpPoint_SuckInCreatures;
        }

        private static void WarpPoint_SuckInCreatures(On.Watcher.WarpPoint.orig_SuckInCreatures orig, WarpPoint self)
        {
            orig(self);

            if (!CreatureWarpState.TryGetLocalNonPlayerAvatar(out Creature avatar))
                return;

            if (avatar.room != self.room || avatar.dead)
                return;

            // Mirror the per-object filter chain vanilla runs before reaching the
            // Player-only block, so the avatar only gets this treatment under the same
            // conditions a Player would (decompile lines 2711-2724).
            if (WarpPoint.BlackListedObjectTypes.Contains(avatar.abstractPhysicalObject.type))
                return;

            float distFromWarp = Vector2.Distance(self.pos, avatar.firstChunk.pos);
            if (distFromWarp > self.creatureSuckRadius || distFromWarp > self.strongPullRadius * 2.5f || self.canWarpToVoidWeaverEnding)
                return;

            bool important = AbstractPhysicalObject.IsObjectImportant(avatar.abstractPhysicalObject, self.room.world);
            if (!avatar.abstractPhysicalObject.IsSameRippleLayer(self.Data.rippleWarp ? 1 : 0) && !important)
                return;

            CreatureWarpState state = CreatureWarpState.Get(avatar);
            state.warpPointCooldown = 80;

            float distFromWarpMain = Vector2.Distance(self.pos, avatar.mainBodyChunk.pos);
            if (distFromWarpMain < self.PullRadius || state.triggeredWarpPoint == self)
            {
                state.standingInWarpPointProtectionTime = 10;
            }
        }
    }
}
