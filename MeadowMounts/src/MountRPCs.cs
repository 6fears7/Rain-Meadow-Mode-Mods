using RainMeadow;

namespace MeadowMounts
{
    public static class MountRPCs
    {
        [RPCMethod]
        public static void MouthSnap(OnlineCreature lizardObj, byte caught)
        {
            if (lizardObj.realizedCreature is not Lizard liz || liz.room == null) return;

            liz.room.PlaySound(SoundID.Lizard_Jaws_Shut_Miss_Creature, liz.mainBodyChunk);
            if (caught != 0) liz.room.PlaySound(SoundID.UI_Multiplayer_Player_Revive, liz.mainBodyChunk);
        }

        [RPCMethod]
        public static void GrabConfirm(OnlineCreature grabberObj)
        {
            if (grabberObj.realizedCreature is not Creature grabber || grabber.room == null) return;

            grabber.room.PlaySound(SoundID.UI_Multiplayer_Player_Revive, grabber.mainBodyChunk);
        }
    }
}
