using Menu.Remix.MixedUI;
using UnityEngine;

namespace MeadowMounts
{
    // Names match the buttons already exposed on Player.InputPackage - kept as plain
    // strings because that's what OpComboBox/Configurable<string> work with in the remix menu.
    // Picking from these (rather than a raw key bind) keeps grab/release riding on the same
    // network-synced input the rest of the mount logic already relies on.
    public static class MountButtons
    {
        public const string Pickup = "Pickup";
        public const string Special = "Special";
        public const string Throw = "Throw";
        public const string Jump = "Jump";
        public const string Map = "Map";

        public static readonly string[] All = { Pickup, Special, Throw, Jump, Map };

        public static bool Held(string button, Player.InputPackage input) => button switch
        {
            Special => input.spec,
            Throw => input.thrw,
            Jump => input.jmp,
            Map => input.mp,
            _ => input.pckp,
        };
    }

    public class MountOptions : OptionInterface
    {
        public static readonly MountOptions Instance = new();

        public readonly Configurable<string> grabButton;
        public readonly Configurable<string> releaseButton;

        private MountOptions()
        {
            grabButton = config.Bind("grabButton", MountButtons.Pickup,
                new ConfigurableInfo("Button used to grab or mount a nearby creature.",
                    new ConfigAcceptableList<string>(MountButtons.All)));

            releaseButton = config.Bind("releaseButton", MountButtons.Pickup,
                new ConfigurableInfo(
                    "Button used to let go of anything you are actively carrying or mouth-holding, " +
                    "or anyone piggybacking on your back. If something is instead riding you as a " +
                    "mount (e.g. you're a lizard being ridden), use Special to shake them of. Use Jump to get yourself out of a mount, " +
                    "grip, mouth, or piggyback.",
                    new ConfigAcceptableList<string>(MountButtons.All)));
        }

        public override void Initialize()
        {
            base.Initialize();

            var tab = new OpTab(this, "Options");
            Tabs = new[] { tab };

            tab.AddItems(
                new OpLabel(20f, 550f, "Meadow Mounts", bigText: true),
                new OpLabel(20f, 500f, "Grab / mount button"),
                new OpComboBox(grabButton, new Vector2(20f, 470f), 150f, MountButtons.All),
                new OpLabel(220f, 500f, "Release / let go button"),
                new OpComboBox(releaseButton, new Vector2(220f, 470f), 150f, MountButtons.All),
                new OpLabelLong(new Vector2(20f, 100f), new Vector2(500f, 260f),
                    "These two cover your own actions when you're grabbing something new and " +
                    "letting go of something you've grabbed.\n\n" +
                    "To buck riders off of you, use Special.\n\n" +
                    "To dismount a creature, use Jump.")
            );
        }
    }
}
