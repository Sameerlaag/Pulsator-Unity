using UnityEngine;
using System;

[RequireComponent(typeof(AudioSource))]
public class RhythmAnalyzer : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;

    [Header("FFT Settings")]
    public int spectrumSize = 1024;
    public FFTWindow fftWindow = FFTWindow.BlackmanHarris;

    [Header("Band Settings (5 lanes)")]
    public float peakSensitivity = 1.35f;   // Higher = fewer beats
    public float baselineSmooth = 0.08f;

    [Header("Note Intensity Settings")]
    public float minIntensity = 0.001f;
    public float maxIntensity = 0.05f;

    // OUTPUT DATA
    [HideInInspector] public float[] spectrum;
    [HideInInspector] public float[] bands = new float[5];
    [HideInInspector] public bool[] peaks = new bool[5];
    [HideInInspector] public int[] intensities = new int[5];

    private AudioMapGenerator audioMapGenerator;
    // Events
    public Action<int, int> OnBeat; // lane 0-4, power 1-5

    // Internals
    private float[] previous = new float[5];

    void Awake()
    {
        spectrum = new float[spectrumSize];
        audioMapGenerator = GetComponent<AudioMapGenerator>();

        if (!audioSource)
            audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (audioMapGenerator == null)
        {
            Debug.LogError("RhythmAnalyzer: AudioMapGenerator is missing!");
            return;
        }

        if (audioSource.clip == null)
        {
            Debug.LogError("RhythmAnalyzer: AudioSource has NO AUDIO CLIP!");
            return;
        }
    }

    void Update()
    {
        AnalyzeMusic();
    }

    // ============================================================
    // 🔥 ANALYSIS PIPELINE
    // ============================================================

    private void AnalyzeMusic()
    {
        // 1. FFT
        audioSource.GetSpectrumData(spectrum, 0, fftWindow);

        // 2. Extract 5 bands
        Create5Bands();

        // 3. Detect peaks (beats)
        DetectPeaks();

        // 4. Compute intensities 1–5
        ComputeIntensity();

        // 5. Fire beat events
        FireBeatEvents();
    }

    // ------------------------------------------------------------
    // 1. SPLIT SPECTRUM INTO 5 BANDS
    // ------------------------------------------------------------

    private void Create5Bands()
    {
        int samplesPerBand = spectrumSize / 5;

        for (int b = 0; b < 5; b++)
        {
            float avg = 0f;

            for (int i = 0; i < samplesPerBand; i++)
                avg += spectrum[b * samplesPerBand + i];

            bands[b] = avg / samplesPerBand;
        }
    }

    // ------------------------------------------------------------
    // 2. PEAK / BEAT DETECTION
    // ------------------------------------------------------------

    private void DetectPeaks()
    {
        for (int i = 0; i < 5; i++)
        {
            peaks[i] = bands[i] > previous[i] * peakSensitivity;

            // smooth baseline
            previous[i] = Mathf.Lerp(previous[i], bands[i], baselineSmooth);
        }
    }

    // ------------------------------------------------------------
    // 3. INTENSITY (1–5)
    // ------------------------------------------------------------

    private void ComputeIntensity()
    {
        for (int i = 0; i < 5; i++)
        {
            float norm = Mathf.InverseLerp(minIntensity, maxIntensity, bands[i]);
            intensities[i] = Mathf.Clamp(Mathf.RoundToInt(norm * 5), 1, 5);
        }
    }

    // ------------------------------------------------------------
    // 4. FIRE EVENTS
    // ------------------------------------------------------------

    private void FireBeatEvents()
    {
        if (OnBeat == null) return;

        for (int lane = 0; lane < 5; lane++)
        {
            if (peaks[lane])
            {
                int power = intensities[lane];
                OnBeat(lane, power);
            }
        }
    }

    // ============================================================
    // 🔍 DEBUG GUI
    // ============================================================

    private void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 300, 25), "RHYTHM ANALYZER DEBUG");

        for (int i = 0; i < 5; i++)
        {
            string text =
                $"Lane {i}:  " +
                $"Band={bands[i]:F4}   " +
                $"Peak={(peaks[i] ? "YES" : "no")}   " +
                $"Power={intensities[i]}";

            GUI.Label(new Rect(10, 40 + i * 20, 400, 20), text);
        }
    }
}
