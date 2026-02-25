using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LOSOverlay.Patches
{
    // Suppress drawing of our planning designations when the player hides them.
    //
    // In RimWorld 1.6, cell designations are batched via Graphics.DrawMeshInstanced
    // inside DesignationManager.DrawDesignations — Designation.DesignationDraw() is
    // only reached for Thing-targeted designations. The instanced path is skipped
    // whenever the list count is 0, so we temporarily clear our three def-lists in a
    // Prefix and restore them in a Postfix, leaving all other designations untouched.
    [HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.DrawDesignations))]
    public static class Patch_DesignationManager_DrawDesignations_Hide
    {
        // __state[0] = saved wall list, [1] = cover, [2] = open; null = nothing hidden.
        static void Prefix(DesignationManager __instance, out List<Designation>[] __state)
        {
            __state = null;
            var hypo = __instance.map?.GetComponent<HypotheticalMapState>();
            if (hypo == null || !hypo.PlanningHidden) return;

            var wallDef  = LOSDesignationDefOf.LOSOverlay_PlanWall;
            var coverDef = LOSDesignationDefOf.LOSOverlay_PlanCover;
            var openDef  = LOSDesignationDefOf.LOSOverlay_PlanOpen;

            var wallList  = __instance.designationsByDef[wallDef];
            var coverList = __instance.designationsByDef[coverDef];
            var openList  = __instance.designationsByDef[openDef];

            // Save copies and empty the live lists so the draw call skips them.
            __state = new List<Designation>[]
            {
                new List<Designation>(wallList),
                new List<Designation>(coverList),
                new List<Designation>(openList),
            };
            wallList.Clear();
            coverList.Clear();
            openList.Clear();
        }

        static void Postfix(DesignationManager __instance, List<Designation>[] __state)
        {
            if (__state == null) return;
            __instance.designationsByDef[LOSDesignationDefOf.LOSOverlay_PlanWall].AddRange(__state[0]);
            __instance.designationsByDef[LOSDesignationDefOf.LOSOverlay_PlanCover].AddRange(__state[1]);
            __instance.designationsByDef[LOSDesignationDefOf.LOSOverlay_PlanOpen].AddRange(__state[2]);
        }
    }

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
                __result = true;
            }
        }
    }
}
