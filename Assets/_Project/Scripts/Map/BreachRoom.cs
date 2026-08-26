using System.Collections.Generic;
using UnityEngine;

namespace HunterVsHider.Map
{
    /// <summary>
    /// Component attached to each procedural Police Breach Room container in the scene hierarchy.
    /// Holds explicit child Transform references for all 9 squad spawn points (SpawnPoint_0 .. SpawnPoint_8).
    /// </summary>
    public class BreachRoom : MonoBehaviour
    {
        [Header("Room Identity")]
        public int roomIndex;
        public string roomName;
        public BreachRoomFacing facing;
        public Vector3 centerPosition;

        [Header("Explicit Spawn Point Transforms")]
        public List<Transform> spawnPoints = new List<Transform>();

        /// <summary>
        /// Returns the explicit Transform for the requested squad slot index (0..8).
        /// </summary>
        public Transform GetSpawnPoint(int slotIndex)
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return transform;
            int clamped = Mathf.Clamp(slotIndex, 0, spawnPoints.Count - 1);
            return spawnPoints[clamped];
        }

        public Vector3 GetSpawnPosition(int slotIndex)
        {
            Transform t = GetSpawnPoint(slotIndex);
            return t != null ? t.position : new Vector3(centerPosition.x, 0.5f, centerPosition.z);
        }

        public Quaternion GetSpawnRotation(int slotIndex)
        {
            Transform t = GetSpawnPoint(slotIndex);
            return t != null ? t.rotation : Quaternion.identity;
        }
    }
}
