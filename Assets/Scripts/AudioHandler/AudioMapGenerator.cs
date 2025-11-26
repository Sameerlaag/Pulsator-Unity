using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Assuming EMapNote, MapData, NoteType, and FFTUtility exist in the project.
// Example definitions (for context, you should use your originals):
/*
public enum NoteType { Standard, Heavy, Special }
[Serializable]
public class EMapNote
{
    public float time;
    public int lane;
    public float power;
    public NoteType type;
}
[Serializable]
public class MapData
{
    public string clipName;
    public List<EMapNote> notes;
}
// Placeholder for the external FFT utility
public static class FFTUtility
{
    public static float[] GetSpectrum(float[] samples)
    {
        // Placeholder implementation - REPLACE with actual FFT logic
        // Must return a float array representing the frequency spectrum.
        return new float[128]; 
    }
}
*/

public class AudioMapGenerator : MonoBehaviour
{
    // --- ORIGINAL FIELDS ---
    [Header("Audio Input")] 
    public AudioSource audioSource;
    // Keeping sampleSize for FFT analysis, but it's less critical for beat detection now
    public int sampleSize = 2048; 

    // --- REVISED TIMING/GENERATION FIELDS ---
    [Header("Map Generation Settings")] 
    [Tooltip("Subdivisions per beat (4 = sixteenth notes, 2 = eighth notes, 1 = quarter notes)")]
    public int subdivisionsPerBeat = 4; // Changed default to 4 for the 1-2-3-4 pattern
    [Tooltip("Offset in seconds if the song doesn't start on beat 0")]
    public float beatOffset = 0f;
    [Tooltip("Target lanes for the pattern (The pattern will cycle across lanes 0 to (Lanes-1))")]
    public int lanes = 5; 
    [Tooltip("Minimum energy required to register a hit (0.0-1.0)")]
    public float minimumEnergy = 0.1f;
    [Tooltip("How much stronger a hit must be to spawn a note (prevents spam)")]
    public float peakSensitivity = 1.5f;
    
    [Header("Note Type Logic")]
    [Tooltip("Red notes spawn every N beats (0 = disabled, 4 = every 4th beat, 8 = every 8th)")]
    public int heavyNoteInterval = 8;
    
    [Header("Output")] 
    public List<EMapNote> mapNotes = new List<EMapNote>();
    public bool generateOnStart = true;
    public bool forceGenerate = false;

    // Output for detected BPM
    public float detectedBPM = 120f; 

    public Action OnMapGenerationStart;
    public Action OnMapGenerationComplete;

    private bool isGenerating = false;

    // --- FILE PATH & LIFECYCLE (UNCHANGED) ---
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
    
    // --- BPM ESTIMATION ---
    
    /// <summary>
    /// A very simple beat detection that relies on autocorrelation of the total energy envelope.
    /// NOTE: This is a placeholder and should be replaced with a robust beat detection algorithm for production.
    /// </summary>
    /// <param name="monoSamples">Mono audio data</param>
    /// <param name="sampleRate">Audio clip sample rate</param>
    /// <returns>Estimated BPM</returns>
    private float EstimateBPM(float[] monoSamples, int sampleRate)
    {
        // For simplicity and speed, we'll only analyze the first 30 seconds
        float analysisDuration = Mathf.Min(30f, (float)monoSamples.Length / sampleRate);
        int analysisSamples = Mathf.FloorToInt(analysisDuration * sampleRate);
        
        if (analysisSamples < sampleRate * 5) return 120f; // Default if too short

        // 1. Create a simple energy envelope
        int windowSize = sampleRate / 10; // 0.1s window for energy calculation
        var energyEnvelope = new List<float>();
        for (int i = 0; i < analysisSamples - windowSize; i += windowSize)
        {
            float energy = 0f;
            for (int j = 0; j < windowSize; j++)
                energy += monoSamples[i + j] * monoSamples[i + j]; // Energy = squared amplitude
            energyEnvelope.Add(energy);
        }

        // 2. Autocorrelation to find periodic peaks (tempo)
        int minLag = Mathf.RoundToInt(60f / 200f / (windowSize / (float)sampleRate)); // Max BPM 200 (Min Period 0.3s)
        int maxLag = Mathf.RoundToInt(60f / 80f / (windowSize / (float)sampleRate));  // Min BPM 80 (Max Period 0.75s)
        
        if (maxLag <= minLag) return 120f; // Safety check

        float maxCorrelation = -1f;
        int bestLag = 0;

        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float correlation = 0f;
            for (int i = 0; i < energyEnvelope.Count - lag; i++)
                correlation += energyEnvelope[i] * energyEnvelope[i + lag];
            
            if (correlation > maxCorrelation)
            {
                maxCorrelation = correlation;
                bestLag = lag;
            }
        }

        if (bestLag > 0)
        {
            float secondsPerLag = bestLag * (windowSize / (float)sampleRate);
            return 60f / secondsPerLag;
        }

        return 120f; // Default BPM
    }

    // --- MAP GENERATION CORE ---

    public IEnumerator GenerateMap()
    {
        if (isGenerating) yield break;
        isGenerating = true;
        OnMapGenerationStart?.Invoke();
        mapNotes.Clear();
        
        AudioClip clip = audioSource.clip;

        // === SAFETY CHECKS & DATA LOADING ===
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
        
        // === DETECT BPM & CALCULATE BEAT GRID ===
        detectedBPM = EstimateBPM(monoSamples, clip.frequency);
        float secondsPerBeat = 60f / detectedBPM;
        float subdivisionInterval = secondsPerBeat / subdivisionsPerBeat;
        int sampleRate = clip.frequency;
        float songDuration = (float)clip.samples / sampleRate;
        
        Debug.Log($"[Beat Map] Detected BPM: {detectedBPM:F2} | Song: {songDuration:F1}s | Grid: {subdivisionInterval:F3}s intervals");
        
        // === ANALYZE ENERGY OVER TIME (For filtering/power only) ===
        // We'll still analyze peaks, but only to decide *if* a note should spawn at a grid time.
        List<BeatHit> rawHits = new List<BeatHit>();
        int stepSize = sampleSize / 2;
        float prevTotalEnergy = 0f;

        for (int pos = 0; pos + sampleSize < monoSamples.Length; pos += stepSize)
        {
            float[] chunk = new float[sampleSize];
            Array.Copy(monoSamples, pos, chunk, 0, sampleSize);
            
            // Re-using FFT for energy, as in the original code
            float[] spectrum = FFTUtility.GetSpectrum(chunk); 
            
            float totalEnergy = 0f;
            for (int i = 0; i < spectrum.Length; i++)
                totalEnergy += spectrum[i];

            float currentTime = (float)pos / sampleRate;

            // Detect energy peaks to filter potential hits
            bool isPeak = totalEnergy > minimumEnergy && 
                          totalEnergy > prevTotalEnergy * peakSensitivity;

            if (isPeak)
            {
                rawHits.Add(new BeatHit 
                { 
                    time = currentTime, 
                    energy = totalEnergy,
                    frequencyBands = null // Frequency bands no longer used for lane assignment
                });
            }
            
            prevTotalEnergy = Mathf.Lerp(prevTotalEnergy, totalEnergy, 0.3f);

            if (pos % (stepSize * 50) == 0) yield return null;
        }
        
        // === QUANTIZE RAW HITS TO BEAT GRID ===
        // We quantize the detected hits to the nearest grid time.
        var quantizedHitsMap = new Dictionary<int, float>(); // index -> energy (stores the max energy for that index)
        
        foreach (var hit in rawHits)
        {
            float adjustedTime = hit.time - beatOffset;
            int subdivIndex = Mathf.RoundToInt(adjustedTime / subdivisionInterval);
            float gridTime = (subdivIndex * subdivisionInterval) + beatOffset;
            
            // Only consider hits that align closely with the grid
            if (Mathf.Abs(gridTime - hit.time) > subdivisionInterval * 0.4f)
                continue;

            // Group by index and keep strongest energy
            if (quantizedHitsMap.ContainsKey(subdivIndex))
            {
                quantizedHitsMap[subdivIndex] = Mathf.Max(quantizedHitsMap[subdivIndex], hit.energy);
            }
            else
            {
                quantizedHitsMap.Add(subdivIndex, hit.energy);
            }
        }
        
        var orderedIndices = quantizedHitsMap.Keys.OrderBy(i => i).ToList();
        
        Debug.Log($"[Beat Map] Quantized to {orderedIndices.Count} grid-aligned hits");

        // === ASSIGN LANES & TYPES (Pattern Logic) ===
        
        // The pattern is '1, 4, 3, 2, 1' for a beat with 4 subdivisions.
        // Lane indices: 0 is the root/first lane. 
        // Example: subdivisionsPerBeat=4, lanes=5. Pattern repeats every 4 steps.
        // Step 0 (Beat 1): Lane 0 (The Root)
        // Step 1: Lane 3
        // Step 2: Lane 2
        // Step 3: Lane 1
        // Step 4 (Beat 2): Lane 0 (The Root)
        //
        // Note: The problem asks for '1, 4, 3, 2, 1' over lanes 1-4.
        // If 'lanes' is the total number of lanes (e.g., 5), we use lanes 0 to (lanes-1).
        // Let's assume the core lanes for the pattern are lanes 0 to (lanes-1).
        
        // The pattern index cycles from 0 to subdivisionsPerBeat - 1
        // We use the pattern '0, L-2, L-3, ..., 1' where L=lanes, and 0 is the main beat lane.
        
        // Lane 0 is the root beat.
        int rootLane = 0; 
        int beatCounter = 0; // Counts every full beat (subdivIndex % subdivisionsPerBeat == 0)

        foreach (var subdivIndex in orderedIndices)
        {
            // The position within the current beat (0 to subdivisionsPerBeat - 1)
            int stepInBeat = subdivIndex % subdivisionsPerBeat;
            float hitEnergy = quantizedHitsMap[subdivIndex];
            
            int targetLane;

            if (stepInBeat == 0)
            {
                // Root beat (the '1' in 1-2-3-4-1) always goes to the first lane (Lane 0).
                targetLane = rootLane;
            }
            else
            {
                // Calculate the pattern for the subdivisions.
                // It should cycle: L-1, L-2, L-3, ... 1 (assuming L lanes)
                // This means the distance from the main beat lane (0) decreases.
                
                // Example for 4 subdivisions, 5 lanes:
                // stepInBeat=1: L-1 = 4 -> Lane 3 (using 0-indexed: 4, 3, 2, 1 pattern over indices 1-4)
                // The pattern must fit into the available lanes (0 to lanes-1).
                
                // Let's create the 'down-up' pattern:
                // Subdivision Index 1 maps to Lane (lanes - 1)
                // Subdivision Index 2 maps to Lane (lanes - 2)
                // ...
                // Subdivision Index (subdivisionsPerBeat - 1) maps to Lane (lanes - (subdivisionsPerBeat - 1))

                // The lane number should correspond to the 'distance' from the root beat in the requested pattern:
                // Beat: 1  (Lane 0)
                // Subdiv 1: 4  (Lane 3, for 5 lanes)
                // Subdiv 2: 3  (Lane 2)
                // Subdiv 3: 2  (Lane 1)
                
                // General formula for the descending pattern (4, 3, 2, 1...):
                // Lane = lanes - stepInBeat - 1
                
                int descendingLane = lanes - stepInBeat - 1;
                
                // Ensure the lane is within bounds [1, lanes-1] and not the root lane (0).
                // Use the descending pattern, clamping to 1 if it goes below
                targetLane = Mathf.Max(1, descendingLane); 
                targetLane = Mathf.Min(lanes - 1, targetLane); 
            }

            // Determine note type (Heavy Note logic)
            NoteType type = NoteType.Standard;
            if (stepInBeat == 0) // Only check for heavy notes on the main beat
            {
                if (heavyNoteInterval > 0 && beatCounter > 0 && beatCounter % heavyNoteInterval == 0)
                {
                    type = NoteType.Heavy;
                }
            }
            
            // Ensure power is correctly clamped
            mapNotes.Add(new EMapNote
            {
                time = (subdivIndex * subdivisionInterval) + beatOffset,
                lane = targetLane,
                power = Mathf.Clamp01(hitEnergy),
                type = type
            });
            
            if (stepInBeat == 0)
            {
                beatCounter++;
            }
        }

        Debug.Log($"[Beat Map] Final map: {mapNotes.Count} notes");
        
        // Print distribution summary
        var laneCounts = mapNotes.GroupBy(n => n.lane).OrderBy(g => g.Key);
        foreach (var group in laneCounts)
            Debug.Log($"  Lane {group.Key}: {group.Count()} notes");
        
        int heavyCount = mapNotes.Count(n => n.type == NoteType.Heavy);
        Debug.Log($"  Heavy (Red): {heavyCount}, Standard: {mapNotes.Count - heavyCount}");

        SaveMapToFile();
        isGenerating = false;
        OnMapGenerationComplete?.Invoke();
    }
    
    // The GetStrongestBand method and helper structs are no longer strictly needed 
    // for the lane assignment but are kept for completeness if you use the original FFT code.
    // They are REMOVED here for the streamlined logic, except for the helper structs.
    
    // === HELPER STRUCTS (KEPT FOR DATA CONTEXT) ===
    private struct BeatHit
    {
        public float time;
        public float energy;
        public float[] frequencyBands;
    }
}