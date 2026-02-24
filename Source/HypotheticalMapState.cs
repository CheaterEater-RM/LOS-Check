using System.Collections.Generic;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// Manages hypothetical terrain for planning mode.
    /// Wall/Cover/Open are tracked as Designations (not Things) so they support
    /// drag-placement and render above fog. Observer positions remain as Things.
    /// </summary>
    public class HypotheticalMapState : MapComponent
    {
        public HashSet<IntVec3> HypotheticalWalls = new HashSet<IntVec3>();
        public HashSet<IntVec3> HypotheticalCover = new HashSet<IntVec3>();
        public HashSet<IntVec3> OpenSpaces = new HashSet<IntVec3>();
        public HashSet<IntVec3> ObserverPositions = new HashSet<IntVec3>();
        public bool CombinedViewActive = false;

        private bool _dirty = true;
        public bool IsDirty { get { return _dirty; } }
        public void MarkDirty() { _dirty = true; }
        public void ClearDirty() { _dirty = false; }

        public HypotheticalMapState(Map map) : base(map) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RebuildFromDesignations();
        }

        public void RebuildFromDesignations()
        {
            HypotheticalWalls.Clear();
            HypotheticalCover.Clear();
            OpenSpaces.Clear();
            if (map == null || map.designationManager == null) return;

            var wallDef = LOSDesignationDefOf.LOSOverlay_PlanWall;
            var coverDef = LOSDesignationDefOf.LOSOverlay_PlanCover;
            var openDef = LOSDesignationDefOf.LOSOverlay_PlanOpen;

            foreach (var des in map.designationManager.AllDesignations)
            {
                if (des.target.HasThing) continue;
                if (des.def == wallDef) HypotheticalWalls.Add(des.target.Cell);
                else if (des.def == coverDef) HypotheticalCover.Add(des.target.Cell);
                else if (des.def == openDef) OpenSpaces.Add(des.target.Cell);
            }
            _dirty = true;
        }

        public bool CellBlocksLOS(IntVec3 cell)
        {
            if (HypotheticalWalls.Contains(cell)) return true;
            if (OpenSpaces.Contains(cell)) return false;
            if (!cell.InBounds(map)) return true;
            var edifice = cell.GetEdifice(map);
            return edifice != null && LOSOverlay_Mod.CoverProvider.BlocksLOS(edifice);
        }

        public bool LOSValidator(IntVec3 cell)
        {
            return !CellBlocksLOS(cell);
        }

        public float GetCoverValueAt(IntVec3 cell)
        {
            var provider = LOSOverlay_Mod.CoverProvider;
            if (HypotheticalWalls.Contains(cell)) return provider.HypotheticalWallValue;
            if (HypotheticalCover.Contains(cell)) return provider.HypotheticalCoverValue;
            if (OpenSpaces.Contains(cell)) return 0f;
            if (!cell.InBounds(map)) return 0f;

            float bestCover = 0f;
            var thingList = cell.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                float cover = provider.GetCoverValue(thingList[i]);
                if (cover > bestCover) bestCover = cover;
            }
            return bestCover;
        }

        public void ClearAll()
        {
            if (map != null && map.designationManager != null)
            {
                var toRemove = new List<Designation>();
                foreach (var des in map.designationManager.AllDesignations)
                {
                    if (des.def == LOSDesignationDefOf.LOSOverlay_PlanWall ||
                        des.def == LOSDesignationDefOf.LOSOverlay_PlanCover ||
                        des.def == LOSDesignationDefOf.LOSOverlay_PlanOpen)
                        toRemove.Add(des);
                }
                foreach (var des in toRemove) map.designationManager.RemoveDesignation(des);
            }
            HypotheticalWalls.Clear();
            HypotheticalCover.Clear();
            OpenSpaces.Clear();
            ObserverPositions.Clear();
            CombinedViewActive = false;
            _dirty = true;
        }

        public override void ExposeData() { base.ExposeData(); }
    }
}