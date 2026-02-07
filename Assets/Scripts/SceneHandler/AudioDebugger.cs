namespace SceneHandler
{
   using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Audio troubleshooting and debugging tool
/// Attach to GameManager to diagnose why audio isn't playing
/// </summary>
public class AudioDebugger : MonoBehaviour
{
    [Header("Audio Source to Debug")]
    [SerializeField] private AudioSource targetAudioSource;
    
    [Header("Debug Options")]
    [SerializeField] private bool autoFindAudioSource = true;
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private bool testPlayOnStart = false;
    
    [Header("Manual Test")]
    [SerializeField] private AudioClip testClip;
    [SerializeField] private bool playTestClip = false;
    
    [Header("Status (READ ONLY)")]
    [SerializeField] private bool hasAudioSource = false;
    [SerializeField] private bool hasAudioClip = false;
    [SerializeField] private bool isPlaying = false;
    [SerializeField] private bool isMuted = false;
    [SerializeField] private float volume = 0f;
    [SerializeField] private float audioListenerVolume = 1f;
    [SerializeField] private int audioListenerCount = 0;
    [SerializeField] private string clipName = "None";
    [SerializeField] private float clipLength = 0f;
    [SerializeField] private float currentTime = 0f;
    
    void Start()
    {
        if (autoFindAudioSource && targetAudioSource == null)
        {
            targetAudioSource = GetComponent<AudioSource>();
            if (targetAudioSource == null)
            {
                targetAudioSource = FindObjectOfType<AudioSource>();
            }
        }
        
        if (testPlayOnStart && targetAudioSource != null)
        {
            Debug.Log("[AudioDebugger] Testing audio on start...");
            TestAudioPlayback();
        }
        
        RunFullDiagnostic();
    }
    
    void Update()
    {
        if (playTestClip)
        {
            playTestClip = false;
            TestAudioPlayback();
        }
        
        UpdateStatus();
        
        if (logEveryFrame)
        {
            LogStatus();
        }
    }
    
    void UpdateStatus()
    {
        if (targetAudioSource == null)
        {
            hasAudioSource = false;
            return;
        }
        
        hasAudioSource = true;
        hasAudioClip = targetAudioSource.clip != null;
        isPlaying = targetAudioSource.isPlaying;
        isMuted = targetAudioSource.mute;
        volume = targetAudioSource.volume;
        
        if (hasAudioClip)
        {
            clipName = targetAudioSource.clip.name;
            clipLength = targetAudioSource.clip.length;
            currentTime = targetAudioSource.time;
        }
        
        // Check audio listener
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        audioListenerCount = listeners.Length;
        if (listeners.Length > 0)
        {
            audioListenerVolume = AudioListener.volume;
        }
    }
    
    void LogStatus()
    {
        if (targetAudioSource == null)
        {
            Debug.LogWarning("[AudioDebugger] No AudioSource found!");
            return;
        }
        
        Debug.Log($"[AudioDebugger] Playing: {isPlaying} | Clip: {clipName} | Time: {currentTime:F2}/{clipLength:F2} | Volume: {volume} | Muted: {isMuted}");
    }
    
    [ContextMenu("Run Full Diagnostic")]
    public void RunFullDiagnostic()
    {
        Debug.Log("========== AUDIO DIAGNOSTIC ==========");
        
        // Check AudioSource
        if (targetAudioSource == null)
        {
            Debug.LogError("❌ NO AUDIO SOURCE FOUND!");
            Debug.Log("Fix: Add an AudioSource component to the GameObject");
            return;
        }
        Debug.Log("✓ AudioSource found");
        
        // Check AudioClip
        if (targetAudioSource.clip == null)
        {
            Debug.LogError("❌ NO AUDIO CLIP ASSIGNED!");
            Debug.Log("Fix: Assign an AudioClip to the AudioSource.clip field");
            return;
        }
        Debug.Log($"✓ AudioClip assigned: {targetAudioSource.clip.name}");
        Debug.Log($"  Clip length: {targetAudioSource.clip.length:F2} seconds");
        Debug.Log($"  Clip channels: {targetAudioSource.clip.channels}");
        Debug.Log($"  Clip frequency: {targetAudioSource.clip.frequency}Hz");
        
        // Check volume
        if (targetAudioSource.volume <= 0f)
        {
            Debug.LogError($"❌ VOLUME IS ZERO! Volume: {targetAudioSource.volume}");
            Debug.Log("Fix: Set AudioSource.volume to 1.0");
        }
        else if (targetAudioSource.volume < 0.5f)
        {
            Debug.LogWarning($"⚠️ Volume is low: {targetAudioSource.volume}");
        }
        else
        {
            Debug.Log($"✓ Volume is good: {targetAudioSource.volume}");
        }
        
        // Check mute
        if (targetAudioSource.mute)
        {
            Debug.LogError("❌ AUDIO SOURCE IS MUTED!");
            Debug.Log("Fix: Uncheck 'Mute' on the AudioSource component");
        }
        else
        {
            Debug.Log("✓ Not muted");
        }
        
        // Check AudioListener
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        if (listeners.Length == 0)
        {
            Debug.LogError("❌ NO AUDIO LISTENER IN SCENE!");
            Debug.Log("Fix: Add an AudioListener to your Main Camera");
        }
        else if (listeners.Length > 1)
        {
            Debug.LogWarning($"⚠️ Multiple AudioListeners found ({listeners.Length})!");
            Debug.Log("This can cause issues. Usually only Main Camera should have an AudioListener.");
        }
        else
        {
            Debug.Log("✓ AudioListener found");
        }
        
        // Check AudioListener volume
        if (AudioListener.volume <= 0f)
        {
            Debug.LogError($"❌ AUDIO LISTENER VOLUME IS ZERO!");
            Debug.Log("Fix: Set AudioListener.volume = 1.0 in Project Settings");
        }
        else
        {
            Debug.Log($"✓ AudioListener volume: {AudioListener.volume}");
        }
        
        // Check playOnAwake
        if (targetAudioSource.playOnAwake)
        {
            Debug.Log($"ℹ️ PlayOnAwake is enabled (will auto-play)");
        }
        else
        {
            Debug.Log($"ℹ️ PlayOnAwake is disabled (must call .Play() manually)");
        }
        
        // Check if playing
        if (targetAudioSource.isPlaying)
        {
            Debug.Log($"✓ Audio IS playing (time: {targetAudioSource.time:F2}s)");
        }
        else
        {
            Debug.LogWarning("⚠️ Audio is NOT playing");
            Debug.Log("Try calling audioSource.Play() to start playback");
        }
        
        // Check spatial settings
        if (targetAudioSource.spatialBlend > 0f)
        {
            Debug.LogWarning($"⚠️ Spatial Blend is {targetAudioSource.spatialBlend} (3D audio)");
            Debug.Log("For music, set Spatial Blend to 0 (2D audio)");
        }
        else
        {
            Debug.Log("✓ Spatial Blend is 0 (2D audio - good for music)");
        }
        
        Debug.Log("======================================");
    }
    
    [ContextMenu("Test Audio Playback")]
    public void TestAudioPlayback()
    {
        if (targetAudioSource == null)
        {
            Debug.LogError("[AudioDebugger] No AudioSource to test!");
            return;
        }
        
        AudioClip clipToPlay = testClip != null ? testClip : targetAudioSource.clip;
        
        if (clipToPlay == null)
        {
            Debug.LogError("[AudioDebugger] No clip to play!");
            return;
        }
        
        Debug.Log($"[AudioDebugger] Testing playback of: {clipToPlay.name}");
        
        targetAudioSource.Stop();
        targetAudioSource.clip = clipToPlay;
        targetAudioSource.volume = 1f;
        targetAudioSource.mute = false;
        targetAudioSource.Play();
        
        Debug.Log($"[AudioDebugger] Play() called. IsPlaying: {targetAudioSource.isPlaying}");
    }
    
    [ContextMenu("Fix Common Issues")]
    public void FixCommonIssues()
    {
        Debug.Log("[AudioDebugger] Attempting to fix common issues...");
        
        if (targetAudioSource == null)
        {
            Debug.LogError("Can't fix - no AudioSource!");
            return;
        }
        
        // Fix volume
        if (targetAudioSource.volume <= 0f)
        {
            targetAudioSource.volume = 1f;
            Debug.Log("✓ Fixed: Set volume to 1.0");
        }
        
        // Fix mute
        if (targetAudioSource.mute)
        {
            targetAudioSource.mute = false;
            Debug.Log("✓ Fixed: Unmuted AudioSource");
        }
        
        // Fix spatial blend
        if (targetAudioSource.spatialBlend > 0f)
        {
            targetAudioSource.spatialBlend = 0f;
            Debug.Log("✓ Fixed: Set to 2D audio");
        }
        
        // Fix AudioListener volume
        if (AudioListener.volume <= 0f)
        {
            AudioListener.volume = 1f;
            Debug.Log("✓ Fixed: Set AudioListener volume to 1.0");
        }
        
        // Add AudioListener if missing
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        if (listeners.Length == 0)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.AddComponent<AudioListener>();
                Debug.Log("✓ Fixed: Added AudioListener to Main Camera");
            }
            else
            {
                Debug.LogWarning("⚠️ Couldn't add AudioListener - no Main Camera found");
            }
        }
        
        Debug.Log("[AudioDebugger] Auto-fix complete!");
        RunFullDiagnostic();
    }
}
}