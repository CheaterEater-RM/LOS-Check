using UnityEngine;
using Verse;

namespace LOSOverlay
{
    public class LOSOverlay_Settings : ModSettings
    {
        public int DefaultRange = 30;
        public float OverlayOpacity = 0.35f;
        public bool ShowOnPawnSelect = false;
        public bool ShowOnTurretSelect = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref DefaultRange, "DefaultRange", 30);
            Scribe_Values.Look(ref OverlayOpacity, "OverlayOpacity", 0.35f);
            Scribe_Values.Look(ref ShowOnPawnSelect, "ShowOnPawnSelect", false);
            Scribe_Values.Look(ref ShowOnTurretSelect, "ShowOnTurretSelect", true);
        }
    }

    public class LOSOverlay_Mod : Mod
    {
        public static LOSOverlay_Settings Settings { get; private set; }
        public static bool CEActive { get; private set; }
        public static ICoverProvider CoverProvider { get; private set; }

        public LOSOverlay_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LOSOverlay_Settings>();
            CEActive = ModsConfig.IsActive("CETeam.CombatExtended");
            if (CEActive)
            {
                Log.Message("[LOS Overlay] Combat Extended detected.");
                CoverProvider = new CECoverProvider();
            }
            else
            {
                Log.Message("[LOS Overlay] Vanilla mode.");
                CoverProvider = new VanillaCoverProvider();
            }
        }

        public override string SettingsCategory() => "LOS Overlay";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label($"Default overlay range: {Settings.DefaultRange}");
            Settings.DefaultRange = (int)listing.Slider(Settings.DefaultRange, 10f, 60f);
            listing.Label($"Overlay opacity: {Settings.OverlayOpacity:P0}");
            Settings.OverlayOpacity = listing.Slider(Settings.OverlayOpacity, 0.1f, 0.9f);
            listing.CheckboxLabeled("Show overlay on pawn select", ref Settings.ShowOnPawnSelect,
                "Automatically show static LOS overlay when selecting a drafted pawn.");
            listing.CheckboxLabeled("Show overlay on turret select", ref Settings.ShowOnTurretSelect,
                "Automatically show static LOS overlay when selecting a turret.");
            listing.End();
        }
    }
}