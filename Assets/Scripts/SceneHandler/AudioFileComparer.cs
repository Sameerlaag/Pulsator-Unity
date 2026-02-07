using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Compares two audio files to find differences that might prevent one from playing
/// </summary>
public class AudioFileComparer : MonoBehaviour
{
    [Header("Audio Files to Compare")]
    [SerializeField] private AudioClip workingClip;
    [SerializeField] private AudioClip brokenClip;
    
    [Header("Test Playback")]
    [SerializeField] private AudioSource testAudioSource;
    [SerializeField] private bool autoCreateAudioSource = true;
    
    [Header("Comparison Results (Read Only)")]
    [SerializeField] private string comparisonSummary = "";
    [SerializeField] private List<string> differences = new List<string>();
    [SerializeField] private List<string> recommendations = new List<string>();
    
    void Start()
    {
        if (autoCreateAudioSource && testAudioSource == null)
        {
            testAudioSource = gameObject.AddComponent<AudioSource>();
            testAudioSource.playOnAwake = false;
            testAudioSource.volume = 1f;
            testAudioSource.spatialBlend = 0f;
        }
    }
    
    [ContextMenu("Compare Audio Files")]
    public void CompareAudioFiles()
    {
        differences.Clear();
        recommendations.Clear();
        
        if (workingClip == null || brokenClip == null)
        {
            Debug.LogError("[AudioComparer] Need both clips assigned!");
            comparisonSummary = "ERROR: Assign both clips";
            return;
        }
        
        Debug.Log("========== AUDIO FILE COMPARISON ==========");
        Debug.Log($"Working: {workingClip.name}");
        Debug.Log($"Broken: {brokenClip.name}");
        Debug.Log("==========================================");
        
        // Compare all properties
        CompareProperty("Length", workingClip.length, brokenClip.length, "seconds");
        CompareProperty("Channels", workingClip.channels, brokenClip.channels, "");
        CompareProperty("Frequency", workingClip.frequency, brokenClip.frequency, "Hz");
        CompareProperty("Samples", workingClip.samples, brokenClip.samples, "");
        CompareProperty("Load State", workingClip.loadState.ToString(), brokenClip.loadState.ToString(), "");
        
        // Check import settings
        #if UNITY_EDITOR
        CompareImportSettings();
        #endif
        
        // Generate summary
        if (differences.Count == 0)
        {
            comparisonSummary = "✓ Files appear identical in properties";
            Debug.Log("✓ No significant differences found in clip properties");
        }
        else
        {
            comparisonSummary = $"⚠️ Found {differences.Count} differences";
            Debug.Log($"⚠️ Found {differences.Count} differences:");
            foreach (string diff in differences)
            {
                Debug.Log($"  - {diff}");
            }
        }
        
        if (recommendations.Count > 0)
        {
            Debug.Log("\n📋 RECOMMENDATIONS:");
            foreach (string rec in recommendations)
            {
                Debug.Log($"  → {rec}");
            }
        }
        
        Debug.Log("==========================================");
    }
    
    void CompareProperty<T>(string name, T working, T broken, string unit)
    {
        bool different = !EqualityComparer<T>.Default.Equals(working, broken);
        
        string workingStr = working.ToString();
        string brokenStr = broken.ToString();
        
        if (!string.IsNullOrEmpty(unit))
        {
            workingStr += unit;
            brokenStr += unit;
        }
        
        Debug.Log($"{name}:");
        Debug.Log($"  Working: {workingStr}");
        Debug.Log($"  Broken:  {brokenStr}");
        
        if (different)
        {
            string diff = $"{name}: Working={workingStr}, Broken={brokenStr}";
            differences.Add(diff);
            Debug.LogWarning($"  ⚠️ DIFFERENT!");
            
            // Add specific recommendations
            AddRecommendation(name, working, broken);
        }
        else
        {
            Debug.Log($"  ✓ Same");
        }
    }
    
    void AddRecommendation<T>(string propertyName, T working, T broken)
    {
        if (propertyName == "Length" && broken.ToString() == "0")
        {
            recommendations.Add("Broken clip has 0 length - file may be corrupted or not fully imported");
            recommendations.Add("Try re-importing the audio file");
        }
        
        if (propertyName == "Load State" && broken.ToString() != "Loaded")
        {
            recommendations.Add($"Broken clip load state is '{broken}' - it's not loaded!");
            recommendations.Add("Check import settings: Enable 'Preload Audio Data'");
        }
        
        if (propertyName == "Channels" && broken.ToString() == "0")
        {
            recommendations.Add("Broken clip has 0 channels - file is corrupted");
            recommendations.Add("Convert audio to WAV or OGG format and re-import");
        }
        
        if (propertyName == "Frequency")
        {
            recommendations.Add("Different sample rates detected");
            recommendations.Add("This shouldn't prevent playback, but keep an eye on it");
        }
    }
    
    #if UNITY_EDITOR
    void CompareImportSettings()
    {
        string workingPath = AssetDatabase.GetAssetPath(workingClip);
        string brokenPath = AssetDatabase.GetAssetPath(brokenClip);
        
        if (string.IsNullOrEmpty(workingPath) || string.IsNullOrEmpty(brokenPath))
        {
            Debug.LogWarning("Can't compare import settings - one or both clips not in project");
            return;
        }
        
        AudioImporter workingImporter = AssetImporter.GetAtPath(workingPath) as AudioImporter;
        AudioImporter brokenImporter = AssetImporter.GetAtPath(brokenPath) as AudioImporter;
        
        if (workingImporter == null || brokenImporter == null)
        {
            Debug.LogWarning("Couldn't get AudioImporter for one or both files");
            return;
        }
        
        Debug.Log("\n=== IMPORT SETTINGS COMPARISON ===");
        
        // Compare load type
        var workingSettings = workingImporter.defaultSampleSettings;
        var brokenSettings = brokenImporter.defaultSampleSettings;
        
        CompareImportProperty("Load Type", workingSettings.loadType, brokenSettings.loadType);
        CompareImportProperty("Compression Format", workingSettings.compressionFormat, brokenSettings.compressionFormat);
        CompareImportProperty("Quality", workingSettings.quality, brokenSettings.quality);
        CompareImportProperty("Preload Audio Data", workingImporter.defaultSampleSettings.preloadAudioData, brokenImporter.defaultSampleSettings.preloadAudioData);
        CompareImportProperty("Load In Background", workingImporter.loadInBackground, brokenImporter.loadInBackground);
        CompareImportProperty("Force To Mono", workingImporter.forceToMono, brokenImporter.forceToMono);
    }
    
    void CompareImportProperty<T>(string name, T working, T broken)
    {
        bool different = !EqualityComparer<T>.Default.Equals(working, broken);
        
        Debug.Log($"{name}:");
        Debug.Log($"  Working: {working}");
        Debug.Log($"  Broken:  {broken}");
        
        if (different)
        {
            differences.Add($"Import: {name} differs");
            Debug.LogWarning($"  ⚠️ DIFFERENT!");
            
            // Add recommendations for import settings
            if (name == "Load Type" && broken.ToString() == "Streaming")
            {
                recommendations.Add("Try changing broken clip's Load Type to 'Decompress On Load'");
            }
            
            if (name == "Preload Audio Data" && broken.ToString() == "False")
            {
                recommendations.Add("Enable 'Preload Audio Data' for the broken clip");
            }
            
            if (name == "Load In Background" && broken.ToString() == "True")
            {
                recommendations.Add("Disable 'Load In Background' for rhythm games (needs instant load)");
            }
        }
        else
        {
            Debug.Log($"  ✓ Same");
        }
    }
    #endif
    
    [ContextMenu("Test Play Working Clip")]
    public void TestPlayWorking()
    {
        if (workingClip == null || testAudioSource == null)
        {
            Debug.LogError("Can't test - missing clip or audio source");
            return;
        }
        
        testAudioSource.Stop();
        testAudioSource.clip = workingClip;
        testAudioSource.Play();
        
        Debug.Log($"[Test] Playing WORKING clip: {workingClip.name}");
        Debug.Log($"[Test] Is playing: {testAudioSource.isPlaying}");
    }
    
    [ContextMenu("Test Play Broken Clip")]
    public void TestPlayBroken()
    {
        if (brokenClip == null || testAudioSource == null)
        {
            Debug.LogError("Can't test - missing clip or audio source");
            return;
        }
        
        testAudioSource.Stop();
        testAudioSource.clip = brokenClip;
        testAudioSource.Play();
        
        Debug.Log($"[Test] Playing BROKEN clip: {brokenClip.name}");
        Debug.Log($"[Test] Is playing: {testAudioSource.isPlaying}");
        
        // Monitor for a few frames
        StartCoroutine(MonitorPlayback(brokenClip.name));
    }
    
    System.Collections.IEnumerator MonitorPlayback(string clipName)
    {
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            Debug.Log($"[Test] Frame {i}: Playing={testAudioSource.isPlaying}, Time={testAudioSource.time:F2}");
        }
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Copy Working Settings to Broken")]
    public void CopyWorkingSettingsToBroken()
    {
        string workingPath = AssetDatabase.GetAssetPath(workingClip);
        string brokenPath = AssetDatabase.GetAssetPath(brokenClip);
        
        if (string.IsNullOrEmpty(workingPath) || string.IsNullOrEmpty(brokenPath))
        {
            Debug.LogError("Can't copy settings - clips not in project");
            return;
        }
        
        AudioImporter workingImporter = AssetImporter.GetAtPath(workingPath) as AudioImporter;
        AudioImporter brokenImporter = AssetImporter.GetAtPath(brokenPath) as AudioImporter;
        
        if (workingImporter == null || brokenImporter == null)
        {
            Debug.LogError("Couldn't get AudioImporter for files");
            return;
        }
        
        Debug.Log($"Copying import settings from {workingClip.name} to {brokenClip.name}...");
        
        // Copy settings
        brokenImporter.defaultSampleSettings = workingImporter.defaultSampleSettings;
        brokenImporter.loadInBackground = workingImporter.loadInBackground;
        brokenImporter.forceToMono = workingImporter.forceToMono;
        
        // Save and reimport
        AssetDatabase.ImportAsset(brokenPath, ImportAssetOptions.ForceUpdate);
        
        Debug.Log("✓ Settings copied and reimported!");
        Debug.Log("Try playing the clip again");
    }
    #endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(AudioFileComparer))]
public class AudioFileComparerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        AudioFileComparer comparer = (AudioFileComparer)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Drag your working clip and broken clip into the fields above, then:\n" +
            "1. Compare them\n" +
            "2. Test play each one\n" +
            "3. Copy working settings to broken",
            MessageType.Info
        );
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Compare Audio Files", GUILayout.Height(30)))
        {
            comparer.CompareAudioFiles();
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Test Play Working"))
        {
            comparer.TestPlayWorking();
        }
        
        if (GUILayout.Button("Test Play Broken"))
        {
            comparer.TestPlayBroken();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        GUI.color = Color.green;
        if (GUILayout.Button("Copy Working Settings → Broken", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Copy Import Settings",
                "This will overwrite the broken clip's import settings with the working clip's settings. Continue?",
                "Yes", "Cancel"))
            {
                comparer.CopyWorkingSettingsToBroken();
            }
        }
        GUI.color = Color.white;
    }
}
#endif
