using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(Terrain))]
public class TerrainPulsator : MonoBehaviour
{
  [Header("Audio")]
    public AudioSource audioSource;

    [Header("Wave Settings")]
    public float maxWaveHeight = 5f;
    public float waveSpeed = 2f;
    public float bassImpact = 3f;
    public float waveFrequency = 0.5f;

    [Header("Spike Settings")]
    public float maxSpikeHeight = 10f;
    public float spikeDecayDistance = 100f;
    public float drumImpact = 5f;
    public float spikeRadius = 10f;

    [Header("Pulse Radius Settings")]
    [Tooltip("Inside this radius, no pulse happens.")]
    public float innerRadius = 10f;

    [Tooltip("From innerRadius to fadeRadius, effect gradually increases.")]
    public float fadeRadius = 15f;

    [Tooltip("Beyond this radius, pulse is at full effect.")]
    public float maxRadius = 20f;

    [Header("Update Settings")]
    public int updateInterval = 1;

    private Terrain terrain;
    private TerrainData terrainData;
    private float[,] baseHeights;
    private float[,] heights;
    private int heightmapWidth;
    private int heightmapHeight;

    private float[] spectrumData = new float[256];
    private int frameCount = 0;

    private Vector3 playerPosition;

    void Start()
    {
        terrain = GetComponent<Terrain>();

        // Clone data so changes don’t persist
        TerrainData clone = Instantiate(terrain.terrainData);
        terrain.terrainData = clone;
        terrainData = clone;

        heightmapWidth = terrainData.heightmapResolution;
        heightmapHeight = terrainData.heightmapResolution;

        baseHeights = terrainData.GetHeights(0, 0, heightmapWidth, heightmapHeight);
        heights = new float[heightmapWidth, heightmapHeight];

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerPosition = player ? player.transform.position :
            new Vector3(terrainData.size.x / 2f, 0, 0);
    }

    void Update()
    {
        frameCount++;
        if (frameCount % updateInterval != 0)
            return;

        if (audioSource && audioSource.isPlaying)
        {
            audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);
            UpdateTerrainHeights();
        }
    }

    void UpdateTerrainHeights()
    {
        float bass = GetBassLevel();
        float drums = GetDrumLevel();

        Vector3 size = terrainData.size;

        for (int y = 0; y < heightmapHeight; y++)
        {
            for (int x = 0; x < heightmapWidth; x++)
            {
                float worldX = (x / (float)heightmapWidth) * size.x;
                float worldZ = (y / (float)heightmapHeight) * size.z;

                float dist = Vector2.Distance(
                    new Vector2(worldX, worldZ),
                    new Vector2(playerPosition.x, playerPosition.z)
                );

                // ----------------------------
                // RADIUS ZONE MULTIPLIER
                // ----------------------------
                float radiusMult = GetRadiusMultiplier(dist);

                // Wave
                float wave = Mathf.Sin(Time.time * waveSpeed + worldZ * waveFrequency)
                             * bass * bassImpact * radiusMult;

                // Spike shape
                float spikePattern = GetSpikePattern(worldX, worldZ, Time.time);

                float spikeHeight = drums * drumImpact * spikePattern * radiusMult;

                float total = baseHeights[y, x] + (wave + spikeHeight) / terrainData.size.y;
                heights[y, x] = Mathf.Clamp01(total);
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }

    // ------------------------
    // RADIUS LOGIC
    // ------------------------
    float GetRadiusMultiplier(float distance)
    {
        if (distance <= innerRadius)
            return 0f;

        if (distance <= fadeRadius)
            return Mathf.InverseLerp(innerRadius, fadeRadius, distance);

        if (distance <= maxRadius)
            return 1f;

        return 1f;
    }

    float GetSpikePattern(float x, float z, float time)
    {
        float gridSize = spikeRadius * 2f;
        float gridX = Mathf.Round(x / gridSize) * gridSize;
        float gridZ = Mathf.Round(z / gridSize) * gridSize;

        float distToCenter = Vector2.Distance(new Vector2(x, z), new Vector2(gridX, gridZ));

        if (distToCenter < spikeRadius)
            return 1f - (distToCenter / spikeRadius);

        return 0f;
    }

    float GetBassLevel()
    {
        float sum = 0f;
        for (int i = 0; i < 5; i++) sum += spectrumData[i];
        return (sum / 5f) * maxWaveHeight;
    }

    float GetDrumLevel()
    {
        float sum = 0f;
        for (int i = 5; i < 20; i++) sum += spectrumData[i];
        return (sum / 15f) * maxSpikeHeight;
    }
}