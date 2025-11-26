using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AudioMapGenerator : MonoBehaviour
{
    [Header("Audio Input")] 
    public AudioSource audioSource;
    public int sampleSize = 1024;

    [Header("Settings")] 
    public int lanes = 5;
    public float sensitivity = 2.0f;
    [Tooltip("If power is above this (0.0-1.0), it becomes a Heavy/Red note")]
    public float heavyThreshold = 0.6f; 

    [Tooltip("Uncheck this if another script calls GenerateMap()")]
    public bool generateOnStart = true; 
    public bool forceGenerate = false; 

    [Header("Output")] 
    public List<EMapNote> mapNotes = new List<EMapNote>();

    public Action OnMapGenerationStart;
    public Action OnMapGenerationComplete;

    private bool isGenerating = false;

    private string SavePath 
    {
        get 
        {
            if (audioSource.clip == null) return "";
            string cleanName = string.Join("_", audioSource.clip.name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(Application.persistentDataPath, $"{cleanName}_Map.json");
        }
    }

    private void Start()
    {
        if (generateOnStart && audioSource.clip != null)
            CheckAndLoadMap();
    }

    [ContextMenu("Open Save Folder")]
    public void OpenSaveFolder() => Application.OpenURL(Application.persistentDataPath);

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
            Debug.Log($"[Map System] Loaded {mapNotes.Count} notes.");
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
        Debug.Log($"[Map System] Saved to: {SavePath}");
    }

    public IEnumerator GenerateMap()
    {
        if (isGenerating) yield break;
        isGenerating = true;
        OnMapGenerationStart?.Invoke();
        mapNotes.Clear();
        
        AudioClip clip = audioSource.clip;

        // 1. Safety Checks & Loading
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

        // 2. Data Extraction
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

        // 3. Analysis Loop
        int freq = clip.frequency;
        float[] currentChunk = new float[sampleSize];
        int stepSize = sampleSize / 2; 
        float[] prevLaneEnergy = new float[lanes];
        int currentPosition = 0;

        while (currentPosition + sampleSize < monoSamples.Length)
        {
            Array.Copy(monoSamples, currentPosition, currentChunk, 0, sampleSize);
            float[] spectrum = FFTUtility.GetSpectrum(currentChunk);
            float[] currentLaneEnergy = new float[lanes];
            
            for (int i = 0; i < spectrum.Length; i++)
            {
                int lane = Mathf.FloorToInt((float)i / spectrum.Length * lanes);
                if(lane >= lanes) lane = lanes - 1;
                currentLaneEnergy[lane] += spectrum[i];
            }

            float currentTime = (float)currentPosition / freq;

            for (int l = 0; l < lanes; l++)
            {
                // Beat Logic
                bool isBeat = currentLaneEnergy[l] > 0.05f && 
                              currentLaneEnergy[l] > prevLaneEnergy[l] * sensitivity;

                if (isBeat)
                {
                    // --- NEW: Determine Type based on Intensity ---
                    float p = Mathf.Clamp01(currentLaneEnergy[l]);
                    NoteType t = (p > heavyThreshold) ? NoteType.Heavy : NoteType.Standard;

                    mapNotes.Add(new EMapNote { time = currentTime, lane = l, power = p, type = t });
                }
                
                prevLaneEnergy[l] = Mathf.Lerp(prevLaneEnergy[l], currentLaneEnergy[l], 0.5f);
            }

            currentPosition += stepSize;
            if (currentPosition % (stepSize * 100) == 0) yield return null; 
        }

        SaveMapToFile();
        isGenerating = false;
        OnMapGenerationComplete?.Invoke();
    }
}
