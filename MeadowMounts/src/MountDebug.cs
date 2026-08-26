using System.Linq;
using RainMeadow;
using UnityEngine;

namespace MeadowMounts
{
    public static class MountDebug
    {
        public const bool Verbose = false;

        private const KeyCode Key = KeyCode.M;

        private static int lastDumpFrame = -1;

        public static void Log(string message)
        {
            if (!Verbose) return;
            Mounts.Log?.LogInfo("[MeadowMounts] " + message);
        }

        public static void MaybeDump(Room? room)
        {
            if (room == null) return;
            if (!Input.GetKeyDown(Key)) return;
            if (Time.frameCount == lastDumpFrame) return;
            lastDumpFrame = Time.frameCount;

            Mounts.Log?.LogInfo("=== MeadowMounts body/mass dump ===");
            foreach (var list in room.physicalObjects)
            {
                foreach (var obj in list)
                {
                    if (obj is not Creature c) continue;
                    if (!CreatureController.creatureControllers.TryGetValue(c, out _)) continue;

                    var chunkRads = string.Join(", ", c.bodyChunks.Select(bc => bc.rad.ToString("0.00")));
                    var connDists = c.bodyChunkConnections != null
                        ? string.Join(", ", c.bodyChunkConnections.Select(cc => cc.distance.ToString("0.00")))
                        : "(none)";
                    var length = MountRules.BodyLength(c);

                    Mounts.Log?.LogInfo(
                        $"{c.GetType().Name,-14} len={length,6:0.00}  mass={c.TotalMass,5:0.00}  " +
                        $"chunks=[{chunkRads}]  conns=[{connDists}]  grasps={c.grasps?.Length ?? 0}");
                }
            }
        }
    }
}
