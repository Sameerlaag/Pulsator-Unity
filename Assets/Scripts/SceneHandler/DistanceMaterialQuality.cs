using UnityEngine;

public class DistanceMaterialQuality : MonoBehaviour
{
    public Camera targetCamera;              // If left empty, will auto-fill main camera
    public Material material;                // Material used on the plane

    [Header("Distance Settings")]
    public float maxDistance = 100f;         // At this distance, quality is lowest
    public float minMipmapBias = 0f;         // Best quality
    public float maxMipmapBias = 2f;         // Lowest quality

    [Header("Optional UV Quality Drop")]
    public bool reduceTiling = false;
    public Vector2 highQualityTiling = new Vector2(1, 1);
    public Vector2 lowQualityTiling = new Vector2(0.2f, 0.2f);

    private void Start()
    {
        if (!targetCamera)
            targetCamera = Camera.main;

        if (!material)
            material = GetComponent<Renderer>().material;
    }

    private void Update()
    {
        float dist = Vector3.Distance(targetCamera.transform.position, transform.position);

        // Normalize distance 0 → 1
        float t = Mathf.Clamp01(dist / maxDistance);

        // Apply mipmap bias for quality degradation
        float bias = Mathf.Lerp(minMipmapBias, maxMipmapBias, t);
        material.SetFloat("_MipmapBias", bias);

        // Optional: reduce tiling to remove detail
        if (reduceTiling)
        {
            Vector2 tiling = Vector2.Lerp(highQualityTiling, lowQualityTiling, t);
            material.mainTextureScale = tiling;
        }
    }
}