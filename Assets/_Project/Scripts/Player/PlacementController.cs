using UnityEngine;
using HunterVsHider.Map;

namespace HunterVsHider.Player
{
    public class PlacementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private GameObject _wallPrefab;
        [SerializeField] private GameObject _visualIndicatorPrefab;

        private GameObject _visualIndicatorInstance;
        private Camera _mainCamera;

        private void Start()
        {
            _mainCamera = Camera.main;

            if (_visualIndicatorPrefab != null)
            {
                _visualIndicatorInstance = Instantiate(_visualIndicatorPrefab);
                // Ensure indicator doesn't block raycasts/collisions
                var colliders = _visualIndicatorInstance.GetComponentsInChildren<Collider2D>();
                foreach(var col in colliders) col.enabled = false;
            }
        }

        private void Update()
        {
            if (_gridManager == null || _mainCamera == null) return;

            // 1. Get mouse world position
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = Mathf.Abs(_mainCamera.transform.position.z); // distance from camera to world plane
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f;

            // 2. Snap to grid position
            Vector2Int gridPos = _gridManager.WorldToGridPosition(mouseWorldPos);
            Vector3 snappedWorldPos = _gridManager.GridToWorldPosition(gridPos);

            // 3. Update visual indicator
            if (_visualIndicatorInstance != null)
            {
                _visualIndicatorInstance.transform.position = snappedWorldPos;
                
                // Change color if cell is occupied
                bool isEmpty = _gridManager.IsCellEmpty(gridPos);
                var spriteRenderer = _visualIndicatorInstance.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = isEmpty ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
                }
            }

            // 4. Place wall on Left Click
            if (Input.GetMouseButtonDown(0))
            {
                if (_gridManager.IsCellEmpty(gridPos))
                {
                    _gridManager.PlaceWall(gridPos, _wallPrefab);
                }
                else
                {
                    Debug.Log("Cell is already occupied!");
                }
            }
        }
    }
}
