using UnityEngine;

using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class TerrainPulsator : MonoBehaviour
{
    public float amplitude = 1f;      // How high the waves go
    public float wavelength = 2f;     // How stretched/compressed the wave is
    public float speed = 1f;          // How fast the wave travels
    public bool useMusic = false;     // Optional: sync to audio

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] vertices;
    public AudioSource music;         // assign if useMusic = true
    private float[] spectrum = new float[64];

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        baseVertices = mesh.vertices;
        vertices = new Vector3[baseVertices.Length];
    }

    void Update()
    {
        float pulse = 1f;

        if (useMusic && music != null)
        {
            music.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            pulse = spectrum[1] * 20f; // adjust multiplier for desired height
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = baseVertices[i];

            // traveling sine wave along z-axis
            v.y = baseVertices[i].y + Mathf.Sin(v.z / wavelength + Time.time * speed) * amplitude * pulse;

            vertices[i] = v;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals(); // important for lighting
    }
}
