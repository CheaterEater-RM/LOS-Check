using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LOSOverlay.Patches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var gizmo in __result) yield return gizmo;
            if (__instance.IsColonistPlayerControlled && __instance.Drafted &&
                __instance.equipment != null && __instance.equipment.Primary != null &&
                __instance.equipment.Primary.def.IsRangedWeapon)
            {
                yield return new Gizmo_LOSMode(__instance);
                yield return new Gizmo_LOSDirection(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Building_Turret), nameof(Building_Turret.GetGizmos))]
    public static class Patch_Turret_GetGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Turret __instance)
        {
            foreach (var gizmo in __result) yield return gizmo;
            if (__instance.Faction == Faction.OfPlayer)
            {
                yield return new Gizmo_LOSMode(__instance);
                yield return new Gizmo_LOSDirection(__instance);
            }
        }
    }
}