using System.Collections.Generic;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// Manages hypothetical terrain for planning mode. Transient — not saved.
    /// </summary>
    public class HypotheticalMapState : MapComponent
    {
        public HashSet<IntVec3> HypotheticalWalls = new HashSet<IntVec3>();
        public HashSet<IntVec3> HypotheticalCover = new HashSet<IntVec3>();
        public HashSet<IntVec3> OpenSpaces = new HashSet<IntVec3>();
        public HashSet<IntVec3> ObserverPositions = new HashSet<IntVec3>();
        public bool CombinedViewActive = false;

        private bool _dirty = true;
        public bool IsDirty => _dirty;
        public void MarkDirty() => _dirty = true;
        public void ClearDirty() => _dirty = false;

        public HypotheticalMapState(Map map) : base(map) { }

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
            HypotheticalWalls.Clear();
            HypotheticalCover.Clear();
            OpenSpaces.Clear();
            ObserverPositions.Clear();
            CombinedViewActive = false;
            _dirty = true;
        }

        public void AddWall(IntVec3 cell)
        {
            HypotheticalCover.Remove(cell); OpenSpaces.Remove(cell);
            HypotheticalWalls.Add(cell); _dirty = true;
        }

        public void AddCover(IntVec3 cell)
        {
            HypotheticalWalls.Remove(cell); OpenSpaces.Remove(cell);
            HypotheticalCover.Add(cell); _dirty = true;
        }

        public void AddOpenSpace(IntVec3 cell)
        {
            HypotheticalWalls.Remove(cell); HypotheticalCover.Remove(cell);
            OpenSpaces.Add(cell); _dirty = true;
        }

        public void RemoveAt(IntVec3 cell)
        {
            HypotheticalWalls.Remove(cell); HypotheticalCover.Remove(cell);
            OpenSpaces.Remove(cell); ObserverPositions.Remove(cell); _dirty = true;
        }

        public override void ExposeData() { base.ExposeData(); }
    }
}