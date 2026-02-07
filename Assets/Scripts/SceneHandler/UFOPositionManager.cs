using System.Collections.Generic;
using UnityEngine;

namespace SceneHandler
{
    /// <summary>
/// Manages UFO positioning to prevent overlaps
/// UFOs smartly position themselves around their target lane
/// </summary>
public class UFOPositionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LaneSystem laneSystem;
    [SerializeField] private UFOController[] allUFOs = new UFOController[3];
    
    [Header("Positioning Settings")]
    [SerializeField] private float ufoSpacing = 4f; // Minimum space between UFOs
    [SerializeField] private float repositionSpeed = 25f; // Fast repositioning
    
    [Header("Formation Patterns")]
    [SerializeField] private FormationType formationType = FormationType.Horizontal;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    
    private Dictionary<UFOController, Vector3> targetPositions = new Dictionary<UFOController, Vector3>();
    
    public enum FormationType
    {
        Horizontal,  // UFOs spread horizontally
        Vertical,    // UFOs spread vertically  
        Arc,         // UFOs form an arc
        Triangle     // UFOs form triangle
    }
    
    void Start()
    {
        if (laneSystem == null)
            laneSystem = FindObjectOfType<LaneSystem>();
        
        // Initialize target positions
        foreach (var ufo in allUFOs)
        {
            if (ufo != null)
                targetPositions[ufo] = ufo.transform.position;
        }
    }
    
    void Update()
    {
        UpdateUFOPositions();
    }
    
    void UpdateUFOPositions()
    {
        // Group UFOs by their target lane
        var ufosByLane = new Dictionary<int, List<UFOController>>();
        
        foreach (var ufo in allUFOs)
        {
            if (ufo == null) continue;
            
            int lane = ufo.GetTargetLane();
            
            if (!ufosByLane.ContainsKey(lane))
                ufosByLane[lane] = new List<UFOController>();
            
            ufosByLane[lane].Add(ufo);
        }
        
        // Calculate positions for each group
        foreach (var kvp in ufosByLane)
        {
            int lane = kvp.Key;
            List<UFOController> ufos = kvp.Value;
            
            if (ufos.Count == 1)
            {
                // Single UFO - position at lane center
                targetPositions[ufos[0]] = laneSystem.GetLaneSpawnPosition(lane);
            }
            else
            {
                // Multiple UFOs - spread them out
                Vector3[] positions = CalculateFormationPositions(lane, ufos.Count);
                
                for (int i = 0; i < ufos.Count; i++)
                {
                    targetPositions[ufos[i]] = positions[i];
                }
            }
        }
        
        // Move UFOs to their target positions
        foreach (var ufo in allUFOs)
        {
            if (ufo == null || !targetPositions.ContainsKey(ufo)) continue;
            
            Vector3 targetPos = targetPositions[ufo];
            Vector3 currentPos = ufo.transform.position;
            
            // Fast lerp to target
            ufo.transform.position = Vector3.Lerp(
                currentPos, 
                targetPos, 
                repositionSpeed * Time.deltaTime
            );
        }
    }
    
    Vector3[] CalculateFormationPositions(int lane, int ufoCount)
    {
        Vector3 centerPos = laneSystem.GetLaneSpawnPosition(lane);
        Vector3[] positions = new Vector3[ufoCount];
        
        switch (formationType)
        {
            case FormationType.Horizontal:
                return CalculateHorizontalFormation(centerPos, ufoCount);
            
            case FormationType.Vertical:
                return CalculateVerticalFormation(centerPos, ufoCount);
            
            case FormationType.Arc:
                return CalculateArcFormation(centerPos, ufoCount);
            
            case FormationType.Triangle:
                return CalculateTriangleFormation(centerPos, ufoCount);
            
            default:
                return CalculateHorizontalFormation(centerPos, ufoCount);
        }
    }
    
    Vector3[] CalculateHorizontalFormation(Vector3 center, int count)
    {
        Vector3[] positions = new Vector3[count];
        
        // Spread horizontally (X axis)
        float totalWidth = (count - 1) * ufoSpacing;
        float startX = center.x - (totalWidth / 2f);
        
        for (int i = 0; i < count; i++)
        {
            positions[i] = new Vector3(
                startX + (i * ufoSpacing),
                center.y,
                center.z
            );
        }
        
        return positions;
    }
    
    Vector3[] CalculateVerticalFormation(Vector3 center, int count)
    {
        Vector3[] positions = new Vector3[count];
        
        // Spread vertically (Y axis)
        float totalHeight = (count - 1) * ufoSpacing;
        float startY = center.y - (totalHeight / 2f);
        
        for (int i = 0; i < count; i++)
        {
            positions[i] = new Vector3(
                center.x,
                startY + (i * ufoSpacing),
                center.z
            );
        }
        
        return positions;
    }
    
    Vector3[] CalculateArcFormation(Vector3 center, int count)
    {
        Vector3[] positions = new Vector3[count];
        
        // Arc pattern (horizontal spread, vertical offset)
        float totalWidth = (count - 1) * ufoSpacing;
        float startX = center.x - (totalWidth / 2f);
        
        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0.5f;
            float arcHeight = Mathf.Sin(t * Mathf.PI) * ufoSpacing;
            
            positions[i] = new Vector3(
                startX + (i * ufoSpacing),
                center.y + arcHeight,
                center.z
            );
        }
        
        return positions;
    }
    
    Vector3[] CalculateTriangleFormation(Vector3 center, int count)
    {
        Vector3[] positions = new Vector3[count];
        
        if (count == 1)
        {
            positions[0] = center;
        }
        else if (count == 2)
        {
            positions[0] = center + Vector3.left * ufoSpacing * 0.5f;
            positions[1] = center + Vector3.right * ufoSpacing * 0.5f;
        }
        else // 3
        {
            positions[0] = center + Vector3.up * ufoSpacing; // Top
            positions[1] = center + new Vector3(-ufoSpacing * 0.5f, -ufoSpacing * 0.5f, 0); // Bottom left
            positions[2] = center + new Vector3(ufoSpacing * 0.5f, -ufoSpacing * 0.5f, 0); // Bottom right
        }
        
        return positions;
    }
    
    // Public method for UFOs to request position at a lane
    public Vector3 RequestPosition(UFOController ufo, int lane)
    {
        if (!targetPositions.ContainsKey(ufo))
            targetPositions[ufo] = laneSystem.GetLaneSpawnPosition(lane);
        
        return targetPositions[ufo];
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;
        
        // Draw target positions
        Gizmos.color = Color.yellow;
        foreach (var kvp in targetPositions)
        {
            if (kvp.Key != null)
            {
                Gizmos.DrawWireSphere(kvp.Value, 0.5f);
                Gizmos.DrawLine(kvp.Key.transform.position, kvp.Value);
            }
        }
    }
}
}