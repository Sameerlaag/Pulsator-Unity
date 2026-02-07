using System;
using UnityEngine;
using System.IO;
using SceneHandler;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Unity Editor tool to import osu!mania beatmaps
/// </summary>
public class OsuManiaImporter : MonoBehaviour
{
    [Header("Import Settings")]
    [Tooltip("Path to the .osu file (can be relative or absolute)")]
    public string osuFilePath = "path/to/beatmap.osz";
    
    [Tooltip("The map to populate with notes")]
    public RhythmMapData targetMap;
    
    [Tooltip("Number of lanes in your game (default 3)")]
    public int targetLaneCount = 3;
    
    [Header("Note Type Mapping")]
    [Tooltip("How to assign note types")]
    public NoteTypeMappingMode mappingMode = NoteTypeMappingMode.CycleByTime;
    
    [Tooltip("For Pattern mode: repeating pattern of note types")]
    public RhythmMapData.RhythmNote.NoteType[] customPattern = new RhythmMapData.RhythmNote.NoteType[] 
    { 
        RhythmMapData.RhythmNote.NoteType.Absorb, 
        RhythmMapData.RhythmNote.NoteType.Shoot, 
        RhythmMapData.RhythmNote.NoteType.Shield 
    };
    
    [Header("Import Options")]
    [Tooltip("Clear existing notes before importing")]
    public bool clearExistingNotes = true;
    
    [Tooltip("Offset to add to all timestamps (in seconds)")]
    public float timeOffset = 0f;
    
    [Header("Audio Import")]
    [Tooltip("Folder containing the audio file (usually the beatmap folder)")]
    public string audioFolder = "";
    
    [Tooltip("Auto-assign audio clip if found")]
    public bool autoAssignAudio = true;
    
    public enum NoteTypeMappingMode
    {
        CycleByTime,      // Alternate types based on timing
        CycleByLane,      // Different type per lane
        Random,           // Random distribution
        Pattern,          // Custom repeating pattern
        AllAbsorb,        // Everything is absorb
        AllShoot,         // Everything is shoot
        AllShield         // Everything is shield
    }

    public void ImportOsuMap()
    {
        if (targetMap == null)
        {
            Debug.LogError("[OsuImporter] No target map assigned!");
            return;
        }
        
        if (string.IsNullOrEmpty(osuFilePath))
        {
            Debug.LogError("[OsuImporter] No .osu file path provided!");
            return;
        }
        
        // Parse the osu file
        var parseResult = OsuManiaParser.ParseOsuFile(osuFilePath, targetLaneCount);
        
        if (!parseResult.success)
        {
            Debug.LogError($"[OsuImporter] Failed to parse: {parseResult.error}");
            return;
        }
        
        // Clear existing notes if requested
        if (clearExistingNotes)
        {
            targetMap.notes.Clear();
        }
        
        // Apply note type mapping
        foreach (var note in parseResult.notes)
        {
            note.type = GetNoteTypeForMapping(note, parseResult.notes.IndexOf(note));
            note.timestamp += timeOffset;
        }
        
        // Add notes to map
        targetMap.notes.AddRange(parseResult.notes);
        
        // Update map metadata
        targetMap.mapName = $"{parseResult.artist} - {parseResult.title}";
        if (parseResult.bpm > 0)
        {
            targetMap.bpm = parseResult.bpm;
        }
        
        // Try to auto-assign audio
        if (autoAssignAudio && !string.IsNullOrEmpty(parseResult.audioFilename))
        {
            TryAssignAudio(parseResult.audioFilename);
        }
        
        #if UNITY_EDITOR
        EditorUtility.SetDirty(targetMap);
        #endif
        
        Debug.Log($"[OsuImporter] ✓ Successfully imported {parseResult.notes.Count} notes!");
        Debug.Log($"[OsuImporter] Map: {parseResult.artist} - {parseResult.title}");
        Debug.Log($"[OsuImporter] BPM: {parseResult.bpm:F1}");
        Debug.Log($"[OsuImporter] Original: {parseResult.keyCount}K → Converted to: {targetLaneCount} lanes");
    }
    
    private RhythmMapData.RhythmNote.NoteType GetNoteTypeForMapping(RhythmMapData.RhythmNote note, int index)
    {
        switch (mappingMode)
        {
            case NoteTypeMappingMode.CycleByTime:
                return (RhythmMapData.RhythmNote.NoteType)(Mathf.FloorToInt(note.timestamp * 2) % 3);
            
            case NoteTypeMappingMode.CycleByLane:
                return (RhythmMapData.RhythmNote.NoteType)(note.lane % 3);
            
            case NoteTypeMappingMode.Random:
                return (RhythmMapData.RhythmNote.NoteType)Random.Range(0, 3);
            
            case NoteTypeMappingMode.Pattern:
                if (customPattern.Length > 0)
                    return customPattern[index % customPattern.Length];
                return RhythmMapData.RhythmNote.NoteType.Absorb;
            
            case NoteTypeMappingMode.AllAbsorb:
                return RhythmMapData.RhythmNote.NoteType.Absorb;
            
            case NoteTypeMappingMode.AllShoot:
                return RhythmMapData.RhythmNote.NoteType.Shoot;
            
            case NoteTypeMappingMode.AllShield:
                return RhythmMapData.RhythmNote.NoteType.Shield;
            
            default:
                return RhythmMapData.RhythmNote.NoteType.Absorb;
        }
    }
    
    private void TryAssignAudio(string audioFilename)
    {
        // Try to find the audio file
        string searchPath = string.IsNullOrEmpty(audioFolder) 
            ? Path.GetDirectoryName(osuFilePath) 
            : audioFolder;
        
        Debug.Log("audioFilename "+ audioFilename);
        
        string audioPath = Path.Combine(searchPath, audioFilename);
        
        if (!File.Exists(audioPath))
        {
            Debug.LogWarning($"[OsuImporter] Audio file not found: {audioPath}");
            return;
        }
        
        #if UNITY_EDITOR
        // Convert to Unity project relative path
        string projectPath = Application.dataPath;
        
        if (audioPath.StartsWith(projectPath))
        {
            // File is inside Unity project
            string relativePath = "Assets" + audioPath.Substring(projectPath.Length);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
            
            if (clip != null)
            {
                targetMap.musicTrack = clip;
                EditorUtility.SetDirty(targetMap);
                Debug.Log($"[OsuImporter] ✓ Assigned audio: {audioFilename}");
            }
            else
            {
                Debug.LogWarning($"[OsuImporter] Found audio file but couldn't load as AudioClip: {relativePath}");
            }
        }
        else
        {
            Debug.LogWarning($"[OsuImporter] Audio file is outside Unity project. Please import it manually: {audioPath}");
        }
        #endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(OsuManiaImporter))]
public class OsuManiaImporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        OsuManiaImporter importer = (OsuManiaImporter)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "How to use:\n" +
            "1. Download an osu!mania beatmap\n" +
            "2. Set the path to the .osu file\n" +
            "3. Assign a RhythmMapData to populate\n" +
            "4. Click Import!",
            MessageType.Info
        );
        
        EditorGUILayout.Space();
        
        GUI.color = Color.green;
        if (GUILayout.Button("Import osu!mania Map", GUILayout.Height(40)))
        {
            importer.ImportOsuMap();
        }
        GUI.color = Color.white;
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Browse for .osu file"))
        {
            string path = EditorUtility.OpenFilePanel("Select osu!mania beatmap", "", "osu");
            if (!string.IsNullOrEmpty(path))
            {
                importer.osuFilePath = path;
                
                // Auto-set audio folder to beatmap directory
                importer.audioFolder = Path.GetDirectoryName(path);
                
                EditorUtility.SetDirty(importer);
            }
        }
    }
}
#endif
