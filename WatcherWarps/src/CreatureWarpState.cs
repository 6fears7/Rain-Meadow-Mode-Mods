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
                && mgm.avatars.Count > 0
                && mgm.avatars[0].realizedCreature is Creature c
                && c is not Player)
            {
                creature = c;
                return true;
            }
            return false;
        }

        // Species-agnostic sibling of TryGetLocalNonPlayerAvatar: the local client's
        // avatar creature whether it's a Player (Slugcat) or not. Used by the
        // phase 7-03 pre-orig WorldSession handoff, which has to cover Player avatars
        // too - vanilla's warpUsed branch never enters the avatar creature itself into
        // the destination WorldSession regardless of species.
        public static bool TryGetLocalAvatar(out AbstractCreature abstractCreature)
        {
            abstractCreature = null!;
            if (Warps.IsMeadowWatcher()
                && OnlineManager.lobby?.gameMode is MeadowGameMode mgm
                && mgm.avatars.Count > 0
                && mgm.avatars[0]?.abstractCreature is AbstractCreature ac)
            {
                abstractCreature = ac;
                return true;
            }
            return false;
        }
    }
}
