using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using RainMeadow;
using UnityEngine;

namespace MeadowMounts
{
    public static class EmoteWheelKeyHooks
    {
        public static void Apply(ManualLogSource log)
        {
            var target = AccessTools.Method(typeof(MeadowEmoteHud), nameof(MeadowEmoteHud.Update));
            if (target == null) throw new MissingMethodException("MeadowEmoteHud.Update not found - Rain Meadow version mismatch?");

            new Harmony(MountsPlugin.ModId).Patch(
                target,
                transpiler: new HarmonyMethod(AccessTools.Method(typeof(EmoteWheelKeyHooks), nameof(UpdateTranspiler))));
        }

        public static bool CombinePickup(bool vanillaPickup)
        {
            try
            {
                var key = MountOptions.Instance.emoteWheelKey.Value;
                if (key == KeyCode.None) return vanillaPickup;
                return Input.GetKey(key);
            }
            catch (Exception e)
            {
                Mounts.Log?.LogError(e);
                return vanillaPickup;
            }
        }

        private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var combine = AccessTools.Method(typeof(EmoteWheelKeyHooks), nameof(CombinePickup));
            bool patched = false;

            foreach (var ci in instructions)
            {
                yield return ci;

                if (!patched
                    && ci.opcode == OpCodes.Ldfld
                    && ci.operand is FieldInfo f
                    && f.Name == "pckp")
                {
                    patched = true;
                    yield return new CodeInstruction(OpCodes.Call, combine);
                }
            }

            if (!patched)
            {
                Mounts.Log?.LogError("EmoteWheelKeyHooks: 'pckp' load not found in MeadowEmoteHud.Update; emote wheel button option inactive");
            }
        }
    }
}
