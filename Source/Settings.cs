using UnityEngine;
using Verse;

namespace LOSOverlay
{
    public class LOSOverlay_Settings : ModSettings
    {
        public int DefaultRange = 50;
        public int DefaultDefensiveRange = 50;
        public float OverlayOpacity = 0.35f;
        public bool ShowOnPawnSelect = false;
        public bool ShowOnTurretSelect = true;

        // --- Vanilla cover color thresholds (cover percentage, 0–1) ---
        // 5 flat color bands with hard cutoffs:
        //   ≤ Thresh1 → green (no cover)
        //   ≤ Thresh2 → yellow-green (low)
        //   ≤ Thresh3 → yellow (moderate)
        //   ≤ Thresh4 → orange (high)
        //   > Thresh4 → red (extreme / walls)
        public float VanillaThresh1 = 0.01f;
        public float VanillaThresh2 = 0.30f;
        public float VanillaThresh3 = 0.50f;
        public float VanillaThresh4 = 0.74f;

        // --- CE cover color thresholds (cover height in meters) ---
        // Same 5 bands, but in meters (fillPercent × 1.75 m/cell).
        public float CEThresh1 = 0.10f;
        public float CEThresh2 = 0.90f;
        public float CEThresh3 = 1.10f;
        public float CEThresh4 = 1.25f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref DefaultRange, "DefaultRange", 50);
            Scribe_Values.Look(ref DefaultDefensiveRange, "DefaultDefensiveRange", 50);
            Scribe_Values.Look(ref OverlayOpacity, "OverlayOpacity", 0.35f);
            Scribe_Values.Look(ref ShowOnPawnSelect, "ShowOnPawnSelect", false);
            Scribe_Values.Look(ref ShowOnTurretSelect, "ShowOnTurretSelect", true);

            Scribe_Values.Look(ref VanillaThresh1, "VanillaThresh1", 0.01f);
            Scribe_Values.Look(ref VanillaThresh2, "VanillaThresh2", 0.30f);
            Scribe_Values.Look(ref VanillaThresh3, "VanillaThresh3", 0.50f);
            Scribe_Values.Look(ref VanillaThresh4, "VanillaThresh4", 0.74f);

            Scribe_Values.Look(ref CEThresh1, "CEThresh1", 0.10f);
            Scribe_Values.Look(ref CEThresh2, "CEThresh2", 0.90f);
            Scribe_Values.Look(ref CEThresh3, "CEThresh3", 1.10f);
            Scribe_Values.Look(ref CEThresh4, "CEThresh4", 1.25f);
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

            // --- General settings ---
            listing.Label($"Default offensive range: {Settings.DefaultRange}");
            Settings.DefaultRange = (int)listing.Slider(Settings.DefaultRange, 10f, 100f);

            listing.Label($"Default defensive range: {Settings.DefaultDefensiveRange}");
            Settings.DefaultDefensiveRange = (int)listing.Slider(Settings.DefaultDefensiveRange, 10f, 100f);

            listing.Label($"Overlay opacity: {Settings.OverlayOpacity:P0}");
            Settings.OverlayOpacity = listing.Slider(Settings.OverlayOpacity, 0.1f, 0.9f);

            listing.CheckboxLabeled("Show overlay on pawn select", ref Settings.ShowOnPawnSelect,
                "Automatically show static LOS overlay when selecting a drafted pawn.");
            listing.CheckboxLabeled("Show overlay on turret select", ref Settings.ShowOnTurretSelect,
                "Automatically show static LOS overlay when selecting a turret.");

            listing.GapLine();

            // --- Cover color thresholds ---
            if (CEActive)
            {
                listing.Label("Cover color bands (CE — height in meters)");
                listing.Gap(4f);

                listing.Label($"Green up to: {Settings.CEThresh1:F2}m");
                Settings.CEThresh1 = listing.Slider(Settings.CEThresh1, 0f, 0.50f);

                listing.Label($"Yellow-green up to: {Settings.CEThresh2:F2}m  (chunks ≈ 0.88m)");
                Settings.CEThresh2 = listing.Slider(Settings.CEThresh2, Settings.CEThresh1, 1.20f);

                listing.Label($"Yellow up to: {Settings.CEThresh3:F2}m  (sandbags/barricades ≈ 1.05m)");
                Settings.CEThresh3 = listing.Slider(Settings.CEThresh3, Settings.CEThresh2, 1.50f);

                listing.Label($"Orange up to: {Settings.CEThresh4:F2}m  (embrasures ≈ 1.23m)");
                Settings.CEThresh4 = listing.Slider(Settings.CEThresh4, Settings.CEThresh3, 2.00f);

                listing.Label("Red: anything above orange threshold");
            }
            else
            {
                listing.Label("Cover color bands (Vanilla — cover percentage)");
                listing.Gap(4f);

                listing.Label($"Green up to: {Settings.VanillaThresh1:P0}");
                Settings.VanillaThresh1 = listing.Slider(Settings.VanillaThresh1, 0f, 0.10f);

                listing.Label($"Yellow-green up to: {Settings.VanillaThresh2:P0}  (chunks = 50%)");
                Settings.VanillaThresh2 = listing.Slider(Settings.VanillaThresh2, Settings.VanillaThresh1, 0.60f);

                listing.Label($"Yellow up to: {Settings.VanillaThresh3:P0}  (sandbags = 55%)");
                Settings.VanillaThresh3 = listing.Slider(Settings.VanillaThresh3, Settings.VanillaThresh2, 0.80f);

                listing.Label($"Orange up to: {Settings.VanillaThresh4:P0}  (walls = 75%)");
                Settings.VanillaThresh4 = listing.Slider(Settings.VanillaThresh4, Settings.VanillaThresh3, 1.00f);

                listing.Label("Red: anything above orange threshold");
            }

            if (listing.ButtonText("Reset color thresholds to defaults"))
            {
                if (CEActive)
                {
                    Settings.CEThresh1 = 0.10f;
                    Settings.CEThresh2 = 0.90f;
                    Settings.CEThresh3 = 1.10f;
                    Settings.CEThresh4 = 1.25f;
                }
                else
                {
                    Settings.VanillaThresh1 = 0.01f;
                    Settings.VanillaThresh2 = 0.30f;
                    Settings.VanillaThresh3 = 0.50f;
                    Settings.VanillaThresh4 = 0.74f;
                }
                OverlayRenderer.ClearMaterialCache();
            }

            listing.End();
        }
    }
}
