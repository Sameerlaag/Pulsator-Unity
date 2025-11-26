using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class AudioMapGenerator : MonoBehaviour
{
    [Header("Audio Input")] 
    public AudioSource audioSource;
    public int sampleSize = 2048;

    [Header("Music Timing")] 
    [Tooltip("Beats per minute of the song")]
    public float BPM = 120f;
    [Tooltip("Subdivisions per beat (4 = sixteenth notes, 2 = eighth notes, 1 = quarter notes)")]
    public int subdivisionsPerBeat = 2;
    [Tooltip("Offset in seconds if the song doesn't start on beat 0")]
    public float beatOffset = 0f;

    [Header("Gameplay Settings")] 
    public int lanes = 5;
    [Tooltip("Minimum energy required to register a hit (0.0-1.0)")]
    public float minimumEnergy = 0.1f;
    [Tooltip("How much stronger a hit must be to spawn a note (prevents spam)")]
    public float peakSensitivity = 1.5f;
    
    [Header("Lane Distribution")]
    [Tooltip("If true, spreads notes across lanes based on frequency. If false, uses pattern logic")]
    public bool useFrequencyMapping = false;
    [Tooltip("Red notes spawn every N beats (0 = disabled, 4 = every 4th beat, 8 = every 8th)")]
    public int heavyNoteInterval = 8;
    [Tooltip("Probability of changing lanes on each note (0.0-1.0)")]
    [Range(0f, 1f)]
    public float laneChangeChance = 0.6f;

    
    [Header("Output")] 
    public List<EMapNote> mapNotes = new List<EMapNote>();
    public bool generateOnStart = true;
    public bool forceGenerate = false;

    public Action OnMapGenerationStart;
    public Action OnMapGenerationComplete;

    private bool isGenerating = false;

    private string SavePath 
    {
        get 
        {
            if (audioSource.clip == null) return "";
            string cleanName = string.Join("_", audioSource.clip.name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(Application.persistentDataPath, $"{cleanName}_BeatMap.json");
        }
    }

    private void Start()
    {
        if (generateOnStart && audioSource.clip != null)
            CheckAndLoadMap();
    }

    [ContextMenu("Open Save Folder")]
    public void OpenSaveFolder() => Application.OpenURL(Application.persistentDataPath);

    [ContextMenu("Force Regenerate")]
    public void ForceRegenerate()
    {
        forceGenerate = true;
        CheckAndLoadMap();
        forceGenerate = false;
    }

    public void CheckAndLoadMap()
    {
        if (isGenerating) return;

        if (!forceGenerate && File.Exists(SavePath))
            LoadMapFromFile();
        else
            StartCoroutine(GenerateMap());
    }

    private void LoadMapFromFile()
    {
        OnMapGenerationStart?.Invoke();
        try 
        {
            string json = File.ReadAllText(SavePath);
            MapData data = JsonUtility.FromJson<MapData>(json);
            mapNotes = data.notes;
            Debug.Log($"[Beat Map] Loaded {mapNotes.Count} notes from {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Load Failed: {e.Message}");
            StartCoroutine(GenerateMap());
            return;
        }
        OnMapGenerationComplete?.Invoke();
    }

    private void SaveMapToFile()
    {
        MapData data = new MapData { clipName = audioSource.clip.name, notes = mapNotes };
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        Debug.Log($"[Beat Map] Saved {mapNotes.Count} notes to: {SavePath}");
    }

    public IEnumerator GenerateMap()
    {
        if (isGenerating) yield break;
        isGenerating = true;
        OnMapGenerationStart?.Invoke();
        mapNotes.Clear();
        
        AudioClip clip = audioSource.clip;

        // === SAFETY CHECKS ===
        if (clip.loadType == AudioClipLoadType.Streaming)
        {
            Debug.LogError("Error: Clip is Streaming. Set to 'Decompress On Load' in Import Settings.");
            isGenerating = false;
            yield break;
        }

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading) yield return null;
        }

        // === EXTRACT MONO AUDIO ===
        float[] monoSamples = new float[clip.samples];
        try 
        {
            float[] allSamples = new float[clip.samples * clip.channels];
            clip.GetData(allSamples, 0);
            for (int i = 0; i < clip.samples; i++) 
                monoSamples[i] = allSamples[i * clip.channels];
        }
        catch (Exception e)
        {
            Debug.LogError($"Data Read Error: {e.Message}");
            isGenerating = false;
            yield break;
        }

        // === CALCULATE BEAT GRID ===
        float secondsPerBeat = 60f / BPM;
        float subdivisionInterval = secondsPerBeat / this.subdivisionsPerBeat;
        int sampleRate = clip.frequency;
        float songDuration = (float)clip.samples / sampleRate;
        
        Debug.Log($"[Beat Map] Song: {songDuration:F1}s | BPM: {BPM} | Grid: {subdivisionInterval:F3}s intervals");

        // === ANALYZE ENERGY OVER TIME ===
        List<BeatHit> rawHits = new List<BeatHit>();
        int stepSize = sampleSize / 2;
        float prevTotalEnergy = 0f;

        for (int pos = 0; pos + sampleSize < monoSamples.Length; pos += stepSize)
        {
            float[] chunk = new float[sampleSize];
            Array.Copy(monoSamples, pos, chunk, 0, sampleSize);
            
            float[] spectrum = FFTUtility.GetSpectrum(chunk);
            
            // Calculate total energy (instead of per-lane)
            float totalEnergy = 0f;
            for (int i = 0; i < spectrum.Length; i++)
                totalEnergy += spectrum[i];

            float currentTime = (float)pos / sampleRate;

            // Detect energy peaks
            bool isPeak = totalEnergy > minimumEnergy && 
                          totalEnergy > prevTotalEnergy * peakSensitivity;

            if (isPeak)
            {
                // Store frequency distribution for lane mapping
                float[] freqBands = new float[lanes];
                for (int i = 0; i < spectrum.Length; i++)
                {
                    int band = Mathf.FloorToInt((float)i / spectrum.Length * lanes);
                    if (band >= lanes) band = lanes - 1;
                    freqBands[band] += spectrum[i];
                }

                rawHits.Add(new BeatHit 
                { 
                    time = currentTime, 
                    energy = totalEnergy,
                    frequencyBands = freqBands
                });
            }
            
            prevTotalEnergy = Mathf.Lerp(prevTotalEnergy, totalEnergy, 0.3f);

            if (pos % (stepSize * 50) == 0) yield return null;
        }

        Debug.Log($"[Beat Map] Detected {rawHits.Count} raw energy peaks");

        // === QUANTIZE TO BEAT GRID ===
        List<QuantizedHit> quantizedHits = new List<QuantizedHit>();
        
        foreach (var hit in rawHits)
        {
            float adjustedTime = hit.time - beatOffset;
            int subdivIndex = Mathf.RoundToInt(adjustedTime / subdivisionInterval);
            float gridTime = (subdivIndex * subdivisionInterval) + beatOffset;
            
            if (Mathf.Abs(gridTime - hit.time) > subdivisionInterval * 0.4f)
                continue;

            quantizedHits.Add(new QuantizedHit
            {
                gridTime = gridTime,
                subdivIndex = subdivIndex,
                energy = hit.energy,
                frequencyBands = hit.frequencyBands
            });
        }

        // Group by time and keep strongest
        var groupedHits = quantizedHits
            .GroupBy(h => h.subdivIndex)
            .Select(g => g.OrderByDescending(h => h.energy).First())
            .OrderBy(h => h.subdivIndex)
            .ToList();

        Debug.Log($"[Beat Map] Quantized to {groupedHits.Count} grid-aligned hits");
// --- New Constants to Define the Musical Structure ---

// Your current setting for subdivisions per beat (e.g., 4 = sixteenth notes)
        int subdivisionsPerBeat = this.subdivisionsPerBeat; // Use the public variable

// Assume 4 beats per measure (common 4/4 time)
        const int BeatsPerMeasure = 4; 
        int subdivisionsPerMeasure = subdivisionsPerBeat * BeatsPerMeasure; 

// Define the fixed lane for the main downbeat (e.g., Lane 0 for the "kick drum")
        const int DownbeatLane = 0;
        // === ASSIGN LANES & TYPES ===
        int currentLane = lanes / 2; // Start in center
// No more 'beatCounter' needed!

        foreach (var hit in groupedHits)
        {
            // --- Determine Musical Position ---
    
            // 1. Is this hit on a major beat? (e.g., the '1', '2', '3', or '4' in a measure)
            bool isMajorBeat = (hit.subdivIndex % subdivisionsPerBeat == 0); 
    
            // 2. Is this hit on the DOWNBEAT? (e.g., the '1' of an N-beat interval)
            // We check the absolute beat count for the heavyNoteInterval logic.
            int absoluteBeatCount = hit.subdivIndex / subdivisionsPerBeat;
    
            // Determine note type
            NoteType type;
            if (heavyNoteInterval > 0 && (absoluteBeatCount % heavyNoteInterval == 0))
            {
                type = NoteType.Heavy; // ALWAYS mark the root note/downbeat with the Heavy type
            }
            else
            {
                type = NoteType.Standard; 
            }
    
            // --- Determine Lane Assignment (Option A Logic) ---
            int targetLane;

            if (type == NoteType.Heavy)
            {
                // 1. HEAVY/ROOT NOTE: Lock to a consistent lane
                targetLane = DownbeatLane; 
            }
            else if (useFrequencyMapping)
            {
                // Make a copy of the bands array
                float[] fillBands = (float[])hit.frequencyBands.Clone();
    
                // Set the energy for the Downbeat Lane (Lane 0) to zero
                // This forces the standard note to be mapped to the strongest *other* frequency.
                fillBands[DownbeatLane] = 0f; 
    
                targetLane = GetStrongestBand(fillBands);
            }
            else
            {
                // 3. STANDARD NOTES (Pattern): Use your original pattern logic
                if (UnityEngine.Random.value < laneChangeChance)
                {
                    // Move to adjacent lane
                    int direction = UnityEngine.Random.value > 0.5f ? 1 : -1;
                    targetLane = Mathf.Clamp(currentLane + direction, 0, lanes - 1);
                }
                else
                {
                    targetLane = currentLane;
                }
            }

            // --- Create and Add Note ---
            mapNotes.Add(new EMapNote
            {
                time = hit.gridTime,
                lane = targetLane,
                power = Mathf.Clamp01(hit.energy),
                type = type
            });

            currentLane = targetLane;
            // beatCounter is removed
        }

        Debug.Log($"[Beat Map] Final map: {mapNotes.Count} notes");
        
        // Print lane distribution
        var laneCounts = mapNotes.GroupBy(n => n.lane).OrderBy(g => g.Key);
        foreach (var group in laneCounts)
            Debug.Log($"  Lane {group.Key}: {group.Count()} notes");
        
        int heavyCount = mapNotes.Count(n => n.type == NoteType.Heavy);
        Debug.Log($"  Heavy (Red): {heavyCount}, Standard: {mapNotes.Count - heavyCount}");

        SaveMapToFile();
        isGenerating = false;
        OnMapGenerationComplete?.Invoke();
    }

    private int GetStrongestBand(float[] bands)
    {
        int strongest = 0;
        float maxEnergy = 0f;
        for (int i = 0; i < bands.Length; i++)
        {
            if (bands[i] > maxEnergy)
            {
                maxEnergy = bands[i];
                strongest = i;
            }
        }
        return strongest;
    }

    // === HELPER STRUCTS ===
    private struct BeatHit
    {
        public float time;
        public float energy;
        public float[] frequencyBands;
    }

    private struct QuantizedHit
    {
        public float gridTime;
        public int subdivIndex;
        public float energy;
        public float[] frequencyBands;
    }
}