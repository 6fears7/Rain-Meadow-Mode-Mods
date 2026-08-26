using RainMeadow;
using static RainMeadow.OnlineState;

namespace MeadowMounts
{
    public class MountsLizardState : RealizedLizardState
    {
        [OnlineFieldHalf(group = "jaw")]
        private float jawOpen;

        public MountsLizardState() { }

        public MountsLizardState(OnlineCreature onlineCreature) : base(onlineCreature)
        {
            if (onlineCreature.apo.realizedObject is Lizard liz) jawOpen = liz.JawOpen;
        }

        public override void ReadTo(OnlineEntity onlineEntity)
        {
            base.ReadTo(onlineEntity);

            if ((onlineEntity as OnlinePhysicalObject)?.apo.realizedObject is Lizard liz)
            {
                liz.JawOpen = jawOpen;
            }
        }
    }
}
