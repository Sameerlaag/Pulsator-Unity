using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SceneHandler
{
     public class BeatmapManager : MonoBehaviour
    {
        [Header("Paths Configuration")]
        [Tooltip("Folder containing .osz files (relative to StreamingAssets)")]
        [SerializeField] private string oszFolderName = "Beatmaps";
        
        [Tooltip("Where to extract .osz contents")]
        [SerializeField] private string extractedFolderName = "ExtractedMaps";
        
        [Header("Game Settings")]
        [SerializeField] private int targetLaneCount = 3;
        
        [Header("Note Type Mapping")]
        [SerializeField] private NoteTypeMappingMode defaultMappingMode = NoteTypeMappingMode.CycleByTime;
        
        [Header("References")]
        [SerializeField] private UI.MainMenuUIManager menuUIManager;
        [SerializeField] private RhythmGameManager gameManager;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        // Runtime data
        private List<RuntimeBeatmap> loadedBeatmaps = new List<RuntimeBeatmap>();
        private string oszFolderPath;
        private string extractedFolderPath;
        private bool isInitialized = false;
        
        public enum NoteTypeMappingMode
        {
            CycleByTime,
            CycleByLane,
            Random,
            AllAbsorb,
            AllShoot,
            AllShield
        }
        
        /// <summary>
        /// Runtime beatmap data structure
        /// </summary>
        [System.Serializable]
        public class RuntimeBeatmap
        {
            public string mapName;
            public string artist;
            public string title;
            public float bpm;
            public int keyCount;
            public string difficulty;
            public string extractedPath;
            public string audioFilePath;
            public RhythmMapData mapData;
            public AudioClip audioClip;
            public bool isLoaded;
            
            public string GetDisplayName()
            {
                if (!string.IsNullOrEmpty(difficulty))
                    return $"{artist} - {title} [{difficulty}]";
                return $"{artist} - {title}";
            }
        }
        
        void Awake()
        {
            SetupPaths();
        }
        
        void Start()
        {
            StartCoroutine(InitializeBeatmaps());
        }
        
        void SetupPaths()
        {
            // Use persistent data path for runtime extraction
            string basePath = Application.persistentDataPath;
            
            // StreamingAssets path for .osz files (read-only on some platforms)
            oszFolderPath = Path.Combine(Application.streamingAssetsPath, oszFolderName);
            
            // Extracted maps go to persistent data (writable)
            extractedFolderPath = Path.Combine(basePath, extractedFolderName);
            
            // Create directories if they don't exist
            if (!Directory.Exists(oszFolderPath))
            {
                Directory.CreateDirectory(oszFolderPath);
                Log($"Created OSZ folder: {oszFolderPath}");
            }
            
            if (!Directory.Exists(extractedFolderPath))
            {
                Directory.CreateDirectory(extractedFolderPath);
                Log($"Created extraction folder: {extractedFolderPath}");
            }
            
            Log($"OSZ Folder: {oszFolderPath}");
            Log($"Extracted Folder: {extractedFolderPath}");
        }
        
        IEnumerator InitializeBeatmaps()
        {
            Log("=== BEATMAP MANAGER INITIALIZATION ===");
            
            // Step 1: Scan for .osz files
            Log("Step 1: Scanning for .osz files...");
            List<string> oszFiles = ScanForOszFiles();
            
            if (oszFiles.Count == 0)
            {
                LogWarning("No .osz files found! Place beatmaps in: " + oszFolderPath);
                FinishInitialization();
                yield break;
            }
            
            Log($"Found {oszFiles.Count} .osz files");
            
            // Step 2: Extract .osz files
            Log("Step 2: Extracting beatmaps...");
            foreach (string oszFile in oszFiles)
            {
                yield return StartCoroutine(ExtractOszFile(oszFile));
            }
            
            // Step 3: Scan extracted folders for .osu files
            Log("Step 3: Scanning for .osu files...");
            List<string> osuFiles = ScanForOsuFiles();
            Log($"Found {osuFiles.Count} .osu files");
            
            // Step 4: Build beatmap data
            Log("Step 4: Building beatmap data...");
            foreach (string osuFile in osuFiles)
            {
                BuildBeatmapFromOsu(osuFile);
                yield return null; // Spread across frames
            }
            
            // Step 5: Load audio clips
            Log("Step 5: Loading audio clips...");
            foreach (var beatmap in loadedBeatmaps)
            {
                if (!string.IsNullOrEmpty(beatmap.audioFilePath))
                {
                    yield return StartCoroutine(LoadAudioClip(beatmap));
                }
            }
            
            Log($"✓ Initialization complete! Loaded {loadedBeatmaps.Count} beatmaps");
            
            FinishInitialization();
        }
        
        List<string> ScanForOszFiles()
        {
            List<string> oszFiles = new List<string>();
            
            if (!Directory.Exists(oszFolderPath))
            {
                LogWarning($"OSZ folder doesn't exist: {oszFolderPath}");
                return oszFiles;
            }
            
            // Look for both .osz and already extracted folders
            string[] files = Directory.GetFiles(oszFolderPath, "*.osz", SearchOption.TopDirectoryOnly);
            oszFiles.AddRange(files);
            
            return oszFiles;
        }
        
        IEnumerator ExtractOszFile(string oszPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(oszPath);
            string targetFolder = Path.Combine(extractedFolderPath, fileName);
            
            // Check if already extracted
            if (Directory.Exists(targetFolder))
            {
                Log($"Already extracted: {fileName}");
                yield break;
            }
            
            Log($"Extracting: {fileName}");
            
            try
            {
                // .osz files are just renamed .zip files
                Directory.CreateDirectory(targetFolder);
                
                // Use System.IO.Compression for extraction
                System.IO.Compression.ZipFile.ExtractToDirectory(oszPath, targetFolder);
                
                Log($"✓ Extracted: {fileName}");
            }
            catch (Exception e)
            {
                LogError($"Failed to extract {fileName}: {e.Message}");
            }
            
            yield return null;
        }
        
        List<string> ScanForOsuFiles()
        {
            List<string> osuFiles = new List<string>();
            
            if (!Directory.Exists(extractedFolderPath))
                return osuFiles;
            
            // Recursively find all .osu files
            string[] files = Directory.GetFiles(extractedFolderPath, "*.osu", SearchOption.AllDirectories);
            osuFiles.AddRange(files);
            
            return osuFiles;
        }
        
        void BuildBeatmapFromOsu(string osuFilePath)
        {
            try
            {
                // Parse the .osu file
                var parseResult = OsuManiaParser.ParseOsuFile(osuFilePath, targetLaneCount);
                
                if (!parseResult.success)
                {
                    LogError($"Failed to parse {Path.GetFileName(osuFilePath)}: {parseResult.error}");
                    return;
                }
                
                // Create runtime beatmap
                RuntimeBeatmap beatmap = new RuntimeBeatmap
                {
                    mapName = Path.GetFileNameWithoutExtension(osuFilePath),
                    artist = parseResult.artist,
                    title = parseResult.title,
                    bpm = parseResult.bpm,
                    keyCount = parseResult.keyCount,
                    extractedPath = Path.GetDirectoryName(osuFilePath),
                    isLoaded = false
                };
                
                // Extract difficulty from filename (e.g., "song [Hard].osu")
                string fileName = Path.GetFileNameWithoutExtension(osuFilePath);
                int bracketStart = fileName.LastIndexOf('[');
                int bracketEnd = fileName.LastIndexOf(']');
                if (bracketStart >= 0 && bracketEnd > bracketStart)
                {
                    beatmap.difficulty = fileName.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                }
                
                // Create RhythmMapData
                RhythmMapData mapData = ScriptableObject.CreateInstance<RhythmMapData>();
                mapData.mapName = beatmap.GetDisplayName();
                mapData.bpm = beatmap.bpm;
                mapData.noteSpeed = 10f; // Default speed, can be customized
                
                // Apply note type mapping
                foreach (var note in parseResult.notes)
                {
                    note.type = GetNoteTypeForMapping(note, parseResult.notes.IndexOf(note));
                    mapData.notes.Add(note);
                }
                
                beatmap.mapData = mapData;
                
                // Find audio file
                if (!string.IsNullOrEmpty(parseResult.audioFilename))
                {
                    string audioPath = Path.Combine(beatmap.extractedPath, parseResult.audioFilename);
                    if (File.Exists(audioPath))
                    {
                        beatmap.audioFilePath = audioPath;
                    }
                    else
                    {
                        LogWarning($"Audio file not found: {parseResult.audioFilename}");
                    }
                }
                
                loadedBeatmaps.Add(beatmap);
                Log($"✓ Built: {beatmap.GetDisplayName()} ({parseResult.notes.Count} notes)");
            }
            catch (Exception e)
            {
                LogError($"Error building beatmap from {osuFilePath}: {e.Message}");
            }
        }
        
        IEnumerator LoadAudioClip(RuntimeBeatmap beatmap)
        {
            if (string.IsNullOrEmpty(beatmap.audioFilePath))
            {
                LogWarning($"No audio path for {beatmap.GetDisplayName()}");
                yield break;
            }
            
            // Determine audio type
            string extension = Path.GetExtension(beatmap.audioFilePath).ToLower();
            AudioType audioType = GetAudioType(extension);
            
            if (audioType == AudioType.UNKNOWN)
            {
                LogWarning($"Unsupported audio format: {extension}");
                yield break;
            }
            
            // Load using UnityWebRequest
            string url = "file://" + beatmap.audioFilePath;
            
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return www.SendWebRequest();
                
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    clip.name = beatmap.GetDisplayName();
                    
                    beatmap.audioClip = clip;
                    beatmap.mapData.musicTrack = clip;
                    beatmap.isLoaded = true;
                    
                    Log($"✓ Loaded audio: {beatmap.GetDisplayName()}");
                }
                else
                {
                    LogError($"Failed to load audio for {beatmap.GetDisplayName()}: {www.error}");
                }
            }
        }
        
        AudioType GetAudioType(string extension)
        {
            switch (extension)
            {
                case ".mp3":
                    return AudioType.MPEG;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".wav":
                    return AudioType.WAV;
                default:
                    return AudioType.UNKNOWN;
            }
        }
        
        RhythmMapData.RhythmNote.NoteType GetNoteTypeForMapping(RhythmMapData.RhythmNote note, int index)
        {
            switch (defaultMappingMode)
            {
                case NoteTypeMappingMode.CycleByTime:
                    return (RhythmMapData.RhythmNote.NoteType)(Mathf.FloorToInt(note.timestamp * 2) % 3);
                
                case NoteTypeMappingMode.CycleByLane:
                    return (RhythmMapData.RhythmNote.NoteType)(note.lane % 3);
                
                case NoteTypeMappingMode.Random:
                    return (RhythmMapData.RhythmNote.NoteType)UnityEngine.Random.Range(0, 3);
                
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
        
        void FinishInitialization()
        {
            isInitialized = true;
            
            // Provide maps to UI
            if (menuUIManager != null)
            {
                List<RhythmMapData> maps = loadedBeatmaps
                    .Where(b => b.isLoaded && b.mapData != null)
                    .Select(b => b.mapData)
                    .ToList();
                
                menuUIManager.SetAvailableSongs(maps);
                Log($"Provided {maps.Count} maps to UI");
            }
            else
            {
                LogWarning("Menu UI Manager not assigned!");
            }
            
            Log("=== READY TO PLAY ===");
        }
        
        // Public API
        
        public List<RuntimeBeatmap> GetLoadedBeatmaps()
        {
            return loadedBeatmaps;
        }
        
        public bool IsInitialized()
        {
            return isInitialized;
        }
        
        public RuntimeBeatmap GetBeatmapByMapData(RhythmMapData mapData)
        {
            return loadedBeatmaps.FirstOrDefault(b => b.mapData == mapData);
        }
        
        // Logging helpers
        void Log(string message)
        {
            if (showDebugLogs)
                Debug.Log($"[BeatmapManager] {message}");
        }
        
        void LogWarning(string message)
        {
            Debug.LogWarning($"[BeatmapManager] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[BeatmapManager] {message}");
        }
    }
}