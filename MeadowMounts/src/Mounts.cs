using System;
using BepInEx.Logging;
using HarmonyLib;
using HUD;
using RainMeadow;
using UnityEngine;

namespace MeadowMounts
{
    public static class Mounts
    {
        internal static ManualLogSource? Log;

        private static Harmony? harmony;

        public static void Apply(ManualLogSource log)
        {
            Log = log;
            harmony = new Harmony(MountsPlugin.ModId);

            var target = AccessTools.Method(typeof(CreatureController), nameof(CreatureController.Update), new[] { typeof(bool) });
            if (target == null) throw new MissingMethodException("CreatureController.Update(bool) not found - Rain Meadow version mismatch?");

            harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(Mounts), nameof(CreatureControllerUpdatePostfix))));

            On.Creature.ReleaseGrasp += Creature_ReleaseGrasp;
            On.Room.Update += Room_Update;
            On.Cicada.CarryObject += Cicada_CarryObject;
            On.Cicada.Stun += Cicada_Stun;
            On.Player.GraphicsModuleUpdated += Player_GraphicsModuleUpdated;
            On.HUD.TextPrompt.EnterGameOverMode += TextPrompt_EnterGameOverMode;

            var realizedState = AccessTools.Method(typeof(AbstractCreatureState), "GetRealizedState");
            if (realizedState == null) throw new MissingMethodException("AbstractCreatureState.GetRealizedState not found - Rain Meadow version mismatch?");

            harmony.Patch(realizedState, prefix: new HarmonyMethod(AccessTools.Method(typeof(Mounts), nameof(GetRealizedStatePrefix))));

            EmoteWheelKeyHooks.Apply(log);
        }

        private static bool GetRealizedStatePrefix(OnlinePhysicalObject onlineObject, ref RealizedPhysicalObjectState __result)
        {
            try
            {
                if (OnlineManager.lobby?.gameMode is MeadowGameMode
                    && onlineObject is OnlineCreature onlineCreature
                    && onlineObject.apo.realizedObject is Lizard)
                {
                    __result = new MountsLizardState(onlineCreature);
                    return false;
                }
            }
            catch (Exception e)
            {
                Log?.LogError(e);
            }

            return true;
        }

        private static void Cicada_CarryObject(On.Cicada.orig_CarryObject orig, Cicada self)
        {
            if (OnlineManager.lobby?.gameMode is MeadowGameMode
                && self.grasps[0]?.grabbed is Creature held)
            {
                try
                {
                    if (MountRules.Resolve(self, held) == MountKind.Carry)
                    {
                        if (Vector2.Distance(self.mainBodyChunk.pos, self.grasps[0].grabbedChunk.pos) > 50f)
                        {
                            self.LoseAllGrasps();
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    Log?.LogError(e);
                }
            }

            orig(self);
        }

        private static void Cicada_Stun(On.Cicada.orig_Stun orig, Cicada self, int st)
        {
            Creature.Grasp? stashed = null;

            if (OnlineManager.lobby?.gameMode is MeadowGameMode)
            {
                try
                {
                    if (self.grasps?.Length > 0 && self.grasps[0]?.grabbed is Creature held
                        && MountRules.Resolve(self, held) == MountKind.Carry)
                    {
                        stashed = self.grasps[0];
                        self.grasps[0] = null;
                    }
                }
                catch (Exception e)
                {
                    Log?.LogError(e);
                }
            }

            try
            {
                orig(self, st);
            }
            finally
            {
                if (stashed != null) self.grasps![0] = stashed;
            }
        }

        private static void Player_GraphicsModuleUpdated(On.Player.orig_GraphicsModuleUpdated orig, Player self, bool actuallyViewed, bool eu)
        {
            Creature.Grasp[]? stashed = null;

            if (OnlineManager.lobby?.gameMode is MeadowGameMode && self.grasps != null)
            {
                try
                {
                    for (int i = 0; i < self.grasps.Length; i++)
                    {
                        if (self.grasps[i]?.grabbed is Creature mount && MountRules.Resolve(self, mount) == MountKind.Ride)
                        {
                            stashed ??= new Creature.Grasp[self.grasps.Length];
                            stashed[i] = self.grasps[i];
                            self.grasps[i] = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log?.LogError(e);
                }
            }

            try
            {
                orig(self, actuallyViewed, eu);
            }
            finally
            {
                if (stashed != null)
                {
                    for (int i = 0; i < stashed.Length; i++)
                    {
                        if (stashed[i] != null) self.grasps![i] = stashed[i];
                    }
                }
            }
        }

        private static void TextPrompt_EnterGameOverMode(On.HUD.TextPrompt.orig_EnterGameOverMode orig, TextPrompt self, Creature.Grasp dependentOnGrasp, int foodInStomach, int deathRoom, Vector2 deathPos)
        {
            try
            {
                if (OnlineManager.lobby?.gameMode is MeadowGameMode)
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Log?.LogError(e);
            }

            orig(self, dependentOnGrasp, foodInStomach, deathRoom, deathPos);
        }

        private static void Creature_ReleaseGrasp(On.Creature.orig_ReleaseGrasp orig, Creature self, int grasp)
        {
            try
            {
                if (!MountControl.AllowRelease(self, grasp)) return;
            }
            catch (Exception e)
            {
                Log?.LogError(e);
            }

            orig(self, grasp);
        }

        private static void CreatureControllerUpdatePostfix(CreatureController __instance)
        {
            if (OnlineManager.lobby?.gameMode is not MeadowGameMode) return;

            try
            {
                MountControl.Update(__instance);
            }
            catch (Exception e)
            {
                Log?.LogError(e);
            }
        }

        private static void Room_Update(On.Room.orig_Update orig, Room self)
        {
            orig(self);

            if (OnlineManager.lobby?.gameMode is not MeadowGameMode) return;

            try
            {
                MountControl.ApplyPhysics(self);
            }
            catch (Exception e)
            {
                Log?.LogError(e);
            }
        }
    }
}
