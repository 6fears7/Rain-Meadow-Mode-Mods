using RainMeadow;
using Watcher;

namespace WatcherWarps
{
    // Generalizes Watcher.WarpPoint.ChangeState's entering-EnterWarp side effects
    // (decompile lines 2618-2636), which unconditionally read the Player-typed
    // playerTriggeredWarpPoint field for the cutscene camera target. Vanilla's own
    // Update() only ever assigns that field for a real Player
    // (Watcher.WarpPoint.decompiled.cs:2386); WarpPointTriggerHooks intentionally
    // cannot assign into it for a non-Player local avatar, so calling orig
    // unmodified in that case is a guaranteed NullReferenceException on
    // playerTriggeredWarpPoint.abstractCreature.
    //
    // Any transition other than entering EnterWarp with playerTriggeredWarpPoint
    // == null - in particular every Player-triggered transition - runs through
    // orig completely untouched. Only the crash-risk case is intercepted, and
    // replicated manually here (mirroring decompile lines 2572-2591 for the
    // "leaving currentState" half and 2618-2636 for the "entering EnterWarp"
    // half), substituting the local non-Player avatar's AbstractCreature for the
    // cutscene camera target.
    public static class ChangeStateHooks
    {
        public static void Apply()
        {
            On.Watcher.WarpPoint.ChangeState += WarpPoint_ChangeState;
        }

        private static void WarpPoint_ChangeState(On.Watcher.WarpPoint.orig_ChangeState orig, WarpPoint self, WarpPoint.State nextState)
        {
            if (nextState != WarpPoint.State.EnterWarp || self.playerTriggeredWarpPoint != null)
            {
                orig(self, nextState);
                return;
            }

            // orig would NRE on playerTriggeredWarpPoint.abstractCreature below -
            // replicate ChangeState's body ourselves for this one transition
            // instead of calling orig.
            CreatureWarpState.TryGetLocalNonPlayerAvatar(out Creature avatar);

            ReplicateLeavingCurrentState(self);

            for (int j = 0; j < self.room.game.Players.Count; j++)
            {
                if (self.room.game.Players[j].realizedCreature != null && self.room.game.Players[j].Room.name == self.room.abstractRoom.name)
                {
                    (self.room.game.Players[j].realizedCreature as Player).warpPointCooldown = 80;
                }
            }
            self.activated = true;
            self.room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Perform_Warp);

            AbstractCreature cutsceneTarget = avatar?.abstractCreature;
            if (cutsceneTarget != null)
            {
                self.room.game.cameras[0].EnterCutsceneMode(cutsceneTarget, RoomCamera.CameraCutsceneType.Standard);
            }
            else
            {
                Warps.Log?.LogWarning("WatcherWarps: entering EnterWarp with no Player and no tracked non-Player avatar to target the cutscene camera - skipping EnterCutsceneMode");
            }

            self.room.game.GetStorySession.pendingWarpObjectsOriginalGravities.Clear();
            self.room.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo.Clear();
            self.activationTime = 0;
            if (self.room.world.game.cameras[0].warpPointTimer == null)
            {
                self.room.world.game.cameras[0].warpPointTimer = new WarpPoint.WarpPointTimer(self.activateAnimationTime * 2f, self);
            }

            self.currentState = nextState;
        }

        // Mirrors decompile lines 2572-2591 - the "leaving currentState" half of
        // ChangeState - since we're bypassing orig entirely for this call.
        //
        // The ExitWarp branch's StopLevitation() loop is Player-list-only and
        // deliberately left that way here - see phase1-06-exit-placement for
        // whether the local non-Player avatar needs an equivalent call.
        private static void ReplicateLeavingCurrentState(WarpPoint self)
        {
            if (self.currentState == WarpPoint.State.ReadyForWarp)
            {
                self.triggerTime = 0f;
            }
            else if (self.currentState == WarpPoint.State.EnterWarp)
            {
                self.activated = false;
                self.activationTime = 0;
                self.room.game.cameras[0].ExitCutsceneMode();
            }
            else if (self.currentState == WarpPoint.State.ExitWarp)
            {
                for (int i = 0; i < self.room.game.Players.Count; i++)
                {
                    if (self.room.game.Players[i].realizedCreature != null)
                    {
                        (self.room.game.Players[i].realizedCreature as Player).StopLevitation();
                    }
                }
            }
            else if (self.currentState == WarpPoint.State.SpawnItems)
            {
                if (self.startedPendingSpawn)
                {
                    self.room.game.GetStorySession.importantWarpPointTransferedEntities.Clear();
                }
                self.startedPendingSpawn = false;
            }
            else if (self.currentState == WarpPoint.State.CoolDown)
            {
                self.successfullWarpCooldownTimer = 0f;
            }
            else if (self.currentState == WarpPoint.State.Sealed)
            {
                if (self.warpTear != null)
                {
                    self.warpTear.weaverBlockAnimation = 0f;
                    self.warpTear.openAnimation = 0f;
                }
                self.warpLocked = false;
                foreach (WeaverThread weaverThread in self.weaverThreads)
                {
                    weaverThread.Destroy();
                }
                self.weaverThreads.Clear();
            }
        }
    }
}
