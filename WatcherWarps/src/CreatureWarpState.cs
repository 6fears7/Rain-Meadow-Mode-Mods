using System.Runtime.CompilerServices;
using RainMeadow;
using Watcher;

namespace WatcherWarps
{
    public class CreatureWarpState
    {
        private static readonly ConditionalWeakTable<Creature, CreatureWarpState> states = new();

        public int warpPointCooldown;
        public int warpExhausionTime;
        public int standingInWarpPointProtectionTime;
        public int performingActivationTimer;
        public WarpPoint? triggeredWarpPoint;

        public static CreatureWarpState Get(Creature creature)
        {
            return states.GetValue(creature, c => new CreatureWarpState());
        }

        public static bool TryGetLocalNonPlayerAvatar(out Creature creature)
        {
            creature = null!;
            if (Warps.IsMeadowWatcher()
                && OnlineManager.lobby?.gameMode is MeadowGameMode mgm
                && mgm.avatars[0].realizedCreature is Creature c
                && c is not Player)
            {
                creature = c;
                return true;
            }
            return false;
        }
    }
}
