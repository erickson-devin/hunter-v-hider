using UnityEngine;

namespace HunterVsHider.Map
{
    public enum CellOccupant
    {
        Empty,
        Wall,
        Trap
    }

    [System.Serializable]
    public struct GridCellData
    {
        public CellOccupant Occupant;
        public GameObject Instance;

        public GridCellData(CellOccupant occupant, GameObject instance)
        {
            Occupant = occupant;
            Instance = instance;
        }
    }
}
