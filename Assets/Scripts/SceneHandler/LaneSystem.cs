using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the 5-lane system - calculates spawn distances and target positions
/// </summary>
public class LaneSystem : MonoBehaviour
{
    [Header("Lane Configuration")]
    [Tooltip("Assign 5 lane objects in order: 0=Far Left, 4=Far Right")]
    [SerializeField] private Transform[] laneMarkers = new Transform[5];
    
    [Header("Player Reference")]
    [SerializeField] private Transform playerTransform;
    
    [Header("Calculated Values (Runtime)")]
    [SerializeField] private float[] laneXPositions = new float[5];
    [SerializeField] private float[] spawnDistances = new float[5];
    [SerializeField] private Vector3 playerTargetPosition;
    
    // Public accessors
    public int LaneCount => laneMarkers.Length;
    public float[] LanePositions => laneXPositions;
    public float[] SpawnDistances => spawnDistances;
    public Vector3 PlayerTargetPosition => playerTargetPosition;
    
    void Awake()
    {
        CalculateLaneSystem();
    }
    
    void OnValidate()
    {
        // Recalculate in editor when values change
        if (Application.isPlaying)
        {
            CalculateLaneSystem();
        }
    }
    
    public void CalculateLaneSystem()
    {
        if (!ValidateSetup())
        {
            Debug.LogError("[LaneSystem] Invalid setup!");
            return;
        }
        
        // Store player's target position (where notes should arrive)
        playerTargetPosition = playerTransform.position;
        
        // Calculate X positions for each lane
        for (int i = 0; i < laneMarkers.Length; i++)
        {
            laneXPositions[i] = laneMarkers[i].position.x;
            
            // Calculate spawn distance (distance from lane marker to player)
            Vector3 lanePos = laneMarkers[i].position;
            Vector3 playerPos = playerTargetPosition;
            
            // Distance is magnitude of vector from player to lane spawn point
            spawnDistances[i] = Vector3.Distance(lanePos, playerPos);
            
            Debug.Log($"[LaneSystem] Lane {i}: X={laneXPositions[i]:F2}, SpawnDist={spawnDistances[i]:F2}");
        }
        
        Debug.Log($"[LaneSystem] Player target position: {playerTargetPosition}");
        Debug.Log("[LaneSystem] ✓ Lane system calculated!");
    }
    
    bool ValidateSetup()
    {
        if (playerTransform == null)
        {
            Debug.LogError("[LaneSystem] No player transform assigned!");
            return false;
        }
        
        if (laneMarkers.Length != 5)
        {
            Debug.LogError($"[LaneSystem] Need exactly 5 lanes, found {laneMarkers.Length}!");
            return false;
        }
        
        for (int i = 0; i < laneMarkers.Length; i++)
        {
            if (laneMarkers[i] == null)
            {
                Debug.LogError($"[LaneSystem] Lane {i} is not assigned!");
                return false;
            }
        }
        
        return true;
    }
    
    // Get spawn position for a specific lane
    public Vector3 GetLaneSpawnPosition(int lane)
    {
        if (lane < 0 || lane >= laneMarkers.Length)
        {
            Debug.LogWarning($"[LaneSystem] Invalid lane {lane}");
            return Vector3.zero;
        }
        
        return laneMarkers[lane].position;
    }
    
    // Get X position for a lane
    public float GetLaneXPosition(int lane)
    {
        if (lane < 0 || lane >= laneXPositions.Length)
        {
            Debug.LogWarning($"[LaneSystem] Invalid lane {lane}");
            return 0f;
        }
        
        return laneXPositions[lane];
    }
    
    // Get spawn distance for a lane
    public float GetSpawnDistance(int lane)
    {
        if (lane < 0 || lane >= spawnDistances.Length)
        {
            Debug.LogWarning($"[LaneSystem] Invalid lane {lane}");
            return 50f; // Fallback
        }
        
        return spawnDistances[lane];
    }
    
    // Helper to get closest lane to a world position
    public int GetClosestLane(Vector3 worldPosition)
    {
        int closestLane = 0;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < laneXPositions.Length; i++)
        {
            float distance = Mathf.Abs(worldPosition.x - laneXPositions[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestLane = i;
            }
        }
        
        return closestLane;
    }
    
    // Visualize lanes in editor
    void OnDrawGizmos()
    {
        if (laneMarkers == null || laneMarkers.Length == 0) return;
        
        // Draw lane spawn points
        Gizmos.color = Color.cyan;
        foreach (Transform lane in laneMarkers)
        {
            if (lane != null)
            {
                Gizmos.DrawWireSphere(lane.position, 1f);
            }
        }
        
        // Draw player position
        if (playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(playerTransform.position, Vector3.one * 2f);
            
            // Draw lines from lanes to player
            Gizmos.color = Color.yellow;
            foreach (Transform lane in laneMarkers)
            {
                if (lane != null)
                {
                    Gizmos.DrawLine(lane.position, playerTransform.position);
                }
            }
        }
    }
}
