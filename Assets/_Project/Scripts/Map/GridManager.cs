using UnityEngine;

namespace HunterVsHider.Map
{
    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float cellSize = 1f;
        public int width = 10;
        public int height = 10;

        public Vector2Int GetGridPosition(Vector2 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x / cellSize);
            int y = Mathf.FloorToInt(worldPosition.y / cellSize);
            return new Vector2Int(x, y);
        }

        public void PlaceWall(Vector2Int gridPosition)
        {
            // Placeholder for instantiating wall prefabs
        }
    }
}
