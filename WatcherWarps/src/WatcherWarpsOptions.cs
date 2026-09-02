using Menu.Remix;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace WatcherWarps
{

    public class WatcherWarpsOptions : OptionInterface
    {
        public static WatcherWarpsOptions? Instance;

        public readonly Configurable<string> OutskirtsDestination;

        public WatcherWarpsOptions()
        {
            OutskirtsDestination = config.Bind(
                "outskirtsDestination",
                OutskirtsDestinations.DefaultKey,
                new ConfigurableInfo("Where the injected Outskirts (SU_C04) warp portal sends you.", null, "", "Outskirts destination"));
        }

        public override void Initialize()
        {
            base.Initialize();

            OpTab warpsTab = new OpTab(this, "Warps");
            Tabs = new[] { warpsTab };

            OpLabel title = new OpLabel(new Vector2(0f, 560f), new Vector2(600f, 30f), "Watcher Warps", FLabelAlignment.Center, true);

            var listItems = new System.Collections.Generic.List<ListItem>();
            var all = OutskirtsDestinations.All;
            for (int i = 0; i < all.Length; i++)
            {
                var d = all[i];
                var item = new ListItem(d.Key, d.Label, i);
                if (!string.IsNullOrEmpty(d.Desc))
                {
                    item.desc = d.Desc;
                }
                listItems.Add(item);
            }

            OpComboBox destinationBox = new OpComboBox(OutskirtsDestination, new Vector2(120f, 470f), 260f, listItems);

            OpLabel destinationLabel = new OpLabel(new Vector2(120f, 500f), new Vector2(260f, 20f), "Outskirts portal destination", FLabelAlignment.Left);

            // >=150px of clear space below the box (470) for the dropdown to open into before
            // the next label at 290.
            OpLabel caveatLabel = new OpLabel(
                new Vector2(40f, 290f),
                new Vector2(520f, 60f),
                "This setting is per-client for the destination of the Outskirts portal",
                FLabelAlignment.Left)
            {
                autoWrap = true
            };

            warpsTab.AddItems(title, destinationLabel, destinationBox, caveatLabel);
        }
    }
}
