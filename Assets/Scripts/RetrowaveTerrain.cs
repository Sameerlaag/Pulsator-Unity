using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RetrowaveTerrain : MonoBehaviour
{
    [Header("Terrain Dimensions")]
    public int width = 80;
    public int depth = 80;
    public float tileSize = 0.5f;
    
    [Header("Player Reference")]
    public Transform player;
    
    [Header("Zone Radii")]
    public float innerDeadZone = 10f; // No effects here
    public float outerRadius = 20f; // Effects end here
    public float bassStartRadius = 10f;
    public float bassEndRadius = 15f;
    public float drumStartRadius = 15f;
    public float drumEndRadius = 20f;
    
    [Header("Wave Settings (10-20 radius)")]
    public float waveSpeed = 3f;
    public float waveFrequency = 1f;
    public float waveHeight = 0.5f;
    
    [Header("Bass Spikes (10-15 radius)")]
    public float bassHeight = 2f;
    public float bassSpikeSize = 1f; // Smaller spikes
    public float bassReactivity = 5f;
    
    [Header("Drum Spikes (15-20 radius)")]
    public float drumHeight = 5f;
    public float drumSpikeSize = 2f; // Larger spikes
    public float drumReactivity = 8f;
    
    [Header("Audio")]
    public AudioSource audioSource;
    
    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] vertices;
    private float[] spectrumData = new float[256];
    private float currentBassLevel = 0f;
    private float currentDrumLevel = 0f;
    
    void Start()
    {
        GenerateTerrain();
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }
    
    void GenerateTerrain()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support for large meshes
        GetComponent<MeshFilter>().mesh = mesh;
        
        // Create vertices
        vertices = new Vector3[(width + 1) * (depth + 1)];
        baseVertices = new Vector3[vertices.Length];
        
        for (int i = 0, z = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++, i++)
            {
                float xPos = (x - width / 2f) * tileSize;
                float zPos = (z - depth / 2f) * tileSize;
                vertices[i] = new Vector3(xPos, 0, zPos);
                baseVertices[i] = vertices[i];
            }
        }
        
        // Create triangles
        int[] triangles = new int[width * depth * 6];
        int vert = 0;
        int tris = 0;
        
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                triangles[tris + 0] = vert + 0;
                triangles[tris + 1] = vert + width + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + width + 1;
                triangles[tris + 5] = vert + width + 2;
                
                vert++;
                tris += 6;
            }
            vert++;
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        // Create UVs for grid texture
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x / (width * tileSize), vertices[i].z / (depth * tileSize));
        }
        mesh.uv = uvs;
    }
    
    void Update()
    {
        if (player == null) return;
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);
            currentBassLevel = GetBassLevel();
            currentDrumLevel = GetDrumLevel();
            UpdateTerrain();
        }
    }
    
    void UpdateTerrain()
    {
        Vector3 playerPos = player.position;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 basePos = baseVertices[i];
            Vector3 worldPos = transform.TransformPoint(basePos);
            
            // Calculate distance from player (XZ plane only)
            float distanceFromPlayer = Vector2.Distance(
                new Vector2(worldPos.x, worldPos.z),
                new Vector2(playerPos.x, playerPos.z)
            );
            
            float height = 0f;
            
            // Only apply effects between inner and outer radius
            if (distanceFromPlayer >= innerDeadZone && distanceFromPlayer <= outerRadius)
            {
                // === CONTINUOUS WAVE (10-20 radius) ===
                float waveZone = Mathf.InverseLerp(innerDeadZone, outerRadius, distanceFromPlayer);
                if (waveZone > 0)
                {
                    float angle = Mathf.Atan2(worldPos.z - playerPos.z, worldPos.x - playerPos.x);
                    float wave = Mathf.Sin(Time.time * waveSpeed + distanceFromPlayer * waveFrequency + angle * 2f);
                    height += wave * waveHeight * waveZone;
                }
                
                // === BASS SPIKES (10-15 radius, smaller) ===
                if (distanceFromPlayer >= bassStartRadius && distanceFromPlayer <= bassEndRadius)
                {
                    float bassZone = Mathf.InverseLerp(bassStartRadius, bassEndRadius, distanceFromPlayer);
                    float bassSpike = GetSpikeHeight(worldPos, playerPos, bassSpikeSize);
                    height += bassSpike * currentBassLevel * bassReactivity * bassZone;
                }
                
                // === DRUM SPIKES (15-20 radius, larger) ===
                if (distanceFromPlayer >= drumStartRadius && distanceFromPlayer <= drumEndRadius)
                {
                    float drumZone = Mathf.InverseLerp(drumStartRadius, drumEndRadius, distanceFromPlayer);
                    float drumSpike = GetSpikeHeight(worldPos, playerPos, drumSpikeSize);
                    height += drumSpike * currentDrumLevel * drumReactivity * drumZone;
                }
            }
            
            vertices[i].y = height;
        }
        
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }
    
    float GetSpikeHeight(Vector3 worldPos, Vector3 playerPos, float spikeSize)
    {
        // Create radial grid of spikes around player
        Vector2 offset = new Vector2(worldPos.x - playerPos.x, worldPos.z - playerPos.z);
        float angle = Mathf.Atan2(offset.y, offset.x);
        float distance = offset.magnitude;
        
        // Create angular segments for spikes
        int segments = 12; // Number of spikes around the circle
        float segmentAngle = (Mathf.PI * 2f) / segments;
        float normalizedAngle = (angle + Mathf.PI) / (Mathf.PI * 2f);
        float spikeIndex = Mathf.Floor(normalizedAngle * segments);
        float spikeAngle = spikeIndex * segmentAngle - Mathf.PI;
        
        // Position within the spike
        Vector2 spikeDirection = new Vector2(Mathf.Cos(spikeAngle), Mathf.Sin(spikeAngle));
        Vector2 spikeCenter = spikeDirection * distance;
        float distToSpikeCenter = Vector2.Distance(offset, spikeCenter);
        
        // Pyramid shape
        float falloff = 1f - Mathf.Clamp01(distToSpikeCenter / spikeSize);
        
        // Also add radial falloff for smoother spikes
        float radialFalloff = Mathf.Clamp01((distance % (spikeSize * 2f)) / (spikeSize * 2f));
        radialFalloff = 1f - Mathf.Abs(radialFalloff - 0.5f) * 2f;
        
        return falloff * radialFalloff;
    }
    
    float GetBassLevel()
    {
        // Bass frequencies: 0-5 bins (roughly 0-250Hz)
        float sum = 0f;
        for (int i = 0; i < 5; i++)
        {
            sum += spectrumData[i];
        }
        return Mathf.Clamp01(sum * 20f); // Normalized and amplified
    }
    
    float GetDrumLevel()
    {
        // Mid frequencies for drums: 5-20 bins (roughly 250-1000Hz)
        float sum = 0f;
        for (int i = 5; i < 20; i++)
        {
            sum += spectrumData[i];
        }
        return Mathf.Clamp01(sum * 15f); // Normalized and amplified
    }
    
    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        
        // Draw debug circles
        Gizmos.color = Color.red;
        DrawCircle(player.position, innerDeadZone);
        
        Gizmos.color = Color.yellow;
        DrawCircle(player.position, bassStartRadius);
        DrawCircle(player.position, bassEndRadius);
        
        Gizmos.color = Color.cyan;
        DrawCircle(player.position, drumStartRadius);
        DrawCircle(player.position, drumEndRadius);
        
        Gizmos.color = Color.white;
        DrawCircle(player.position, outerRadius);
    }
    
    void DrawCircle(Vector3 center, float radius)
    {
        int segments = 32;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        
        for (int i = 1; i <= segments; i++)
        {
            angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(lastPoint, newPoint);
            lastPoint = newPoint;
        }
    }
}