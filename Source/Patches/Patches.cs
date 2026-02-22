using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LOSOverlay.Patches
{
    // Drafted pawns: patch GetGizmos directly since Pawn overrides it.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var gizmo in __result) yield return gizmo;
            if (__instance.IsColonistPlayerControlled && __instance.Drafted)
                yield return new Gizmo_LOSMode(__instance);
        }
    }

    // Turrets: Building_Turret no longer overrides GetGizmos in 1.6, so we
    // can't patch it directly. Instead we use a ThingComp added via XML
    // (see CompLOSMode / PlanningMarkers.xml) which delivers gizmos through
    // the standard CompGetGizmosExtra path.

    // PlanningMarker fog bypass: SelectableByMapClick hard-rejects fogged cells
    // with no override hook. We postfix it to return true for our marker.
    [HarmonyPatch(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.SelectableByMapClick))]
    public static class Patch_SelectableByMapClick_PlanningMarker
    {
        static void Postfix(Thing t, ref bool __result)
        {
            if (t is PlanningMarker)
            {
                Log.Message($"[LOS] SelectableByMapClick called for PlanningMarker at {t.Position}, fogged={t.Position.Fogged(t.Map)}, result before={__result}");
                __result = true;
            }
        }
    }
}
