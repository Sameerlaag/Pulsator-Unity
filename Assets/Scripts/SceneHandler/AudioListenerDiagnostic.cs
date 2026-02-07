using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(AudioListenerDiagnostic))]
public class AudioListenerDiagnosticEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        AudioListenerDiagnostic diagnostic = (AudioListenerDiagnostic)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "If audio is playing but you can't hear it, it's almost always an AudioListener problem.\n\n" +
            "Click 'Auto Fix' to automatically fix common issues.",
            MessageType.Info
        );
        
        EditorGUILayout.Space();
        
        GUI.color = Color.green;
        if (GUILayout.Button("Auto Fix Audio Listener", GUILayout.Height(40)))
        {
            diagnostic.AutoFix();
        }
        GUI.color = Color.white;
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Run Diagnostic"))
        {
            diagnostic.RunDiagnostic();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Test Audio (Beep)"))
        {
            diagnostic.TestAudioBeep();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Quick Checks:\n" +
            "• Is your computer volume up?\n" +
            "• Are headphones/speakers connected?\n" +
            "• Did you click in the Game view window?\n" +
            "• Unity mixer volume not muted?",
            MessageType.None
        );
    }
}
#endif

public class AudioListenerDiagnostic : MonoBehaviour
{
    [Header("Auto-Fix Options")]
    [SerializeField] private bool autoFixOnStart = true;
    [SerializeField] private bool addListenerToMainCamera = true;
    
    [Header("Status (Read Only)")]
    [SerializeField] private int listenerCount = 0;
    [SerializeField] private float globalVolume = 1f;
    [SerializeField] private bool hasMainCamera = false;
    [SerializeField] private bool mainCameraHasListener = false;
    
    void Start()
    {
        if (autoFixOnStart)
        {
            AutoFix();
        }
        else
        {
            RunDiagnostic();
        }
    }
    
    [ContextMenu("Run Diagnostic")]
    public void RunDiagnostic()
    {
        Debug.Log("========== AUDIO LISTENER DIAGNOSTIC ==========");
        
        // Find all AudioListeners
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        listenerCount = listeners.Length;
        
        Debug.Log($"AudioListeners found: {listenerCount}");
        
        if (listenerCount == 0)
        {
            Debug.LogError("❌ NO AUDIO LISTENER IN SCENE!");
            Debug.LogError("This is why you can't hear anything!");
            Debug.LogError("Fix: Add AudioListener component to Main Camera");
            Debug.LogError("Or click 'Auto Fix' button");
        }
        else if (listenerCount == 1)
        {
            Debug.Log("✓ Exactly one AudioListener (correct)");
            AudioListener listener = listeners[0];
            Debug.Log($"  Location: {listener.gameObject.name}");
            Debug.Log($"  Position: {listener.transform.position}");
            Debug.Log($"  Active: {listener.enabled}");
            
            if (!listener.enabled)
            {
                Debug.LogError("❌ AudioListener is DISABLED!");
                Debug.LogError($"Fix: Enable AudioListener on {listener.gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Multiple AudioListeners found ({listenerCount})");
            Debug.LogWarning("This can cause audio issues!");
            Debug.LogWarning("Should only have ONE AudioListener (usually on Main Camera)");
            
            foreach (var listener in listeners)
            {
                Debug.Log($"  - {listener.gameObject.name} (enabled: {listener.enabled})");
            }
        }
        
        // Check global volume
        globalVolume = AudioListener.volume;
        Debug.Log($"AudioListener.volume (global): {globalVolume}");
        
        if (globalVolume <= 0f)
        {
            Debug.LogError("❌ AUDIO LISTENER VOLUME IS ZERO!");
            Debug.LogError("This is why you can't hear anything!");
            Debug.LogError("Fix: Set AudioListener.volume = 1.0");
        }
        else if (globalVolume < 0.5f)
        {
            Debug.LogWarning($"⚠️ AudioListener volume is low: {globalVolume}");
        }
        else
        {
            Debug.Log("✓ AudioListener volume is good");
        }
        
        // Check Main Camera
        Camera mainCam = Camera.main;
        hasMainCamera = mainCam != null;
        
        if (mainCam == null)
        {
            Debug.LogWarning("⚠️ No Main Camera found (tag 'MainCamera' not set)");
        }
        else
        {
            mainCameraHasListener = mainCam.GetComponent<AudioListener>() != null;
            
            if (mainCameraHasListener)
            {
                Debug.Log("✓ Main Camera has AudioListener");
            }
            else
            {
                Debug.LogWarning("⚠️ Main Camera doesn't have AudioListener");
                Debug.LogWarning("Usually AudioListener should be on the camera");
            }
        }
        
        // Check audio settings
        AudioConfiguration config = AudioSettings.GetConfiguration();
        Debug.Log($"Audio Configuration:");
        Debug.Log($"  Sample Rate: {config.sampleRate}Hz");
        Debug.Log($"  DSP Buffer Size: {config.dspBufferSize}");
        Debug.Log($"  Speaker Mode: {config.speakerMode}");
        
        // Check if computer audio is muted (can't detect this, but remind user)
        Debug.Log("\n⚠️ ALSO CHECK:");
        Debug.Log("  - Computer system volume (not muted?)");
        Debug.Log("  - Headphones/speakers connected and on?");
        Debug.Log("  - Unity Game view has audio focus?");
        Debug.Log("  - Try clicking in Game view window");
        
        Debug.Log("===============================================");
    }
    
    [ContextMenu("Auto Fix")]
    public void AutoFix()
    {
        Debug.Log("========== AUTO-FIXING AUDIO LISTENER ==========");
        
        bool fixedSomething = false;
        
        // Fix 1: Global volume
        if (AudioListener.volume <= 0f)
        {
            AudioListener.volume = 1f;
            Debug.Log("✓ Fixed: Set AudioListener.volume to 1.0");
            fixedSomething = true;
        }
        
        // Fix 2: Find all listeners
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        
        if (listeners.Length == 0)
        {
            Debug.Log("No AudioListener found - adding one...");
            
            // Try to add to Main Camera
            Camera mainCam = Camera.main;
            if (mainCam != null && addListenerToMainCamera)
            {
                mainCam.gameObject.AddComponent<AudioListener>();
                Debug.Log($"✓ Fixed: Added AudioListener to {mainCam.gameObject.name}");
                fixedSomething = true;
            }
            else
            {
                // Create a dedicated listener object
                GameObject listenerObj = new GameObject("AudioListener");
                listenerObj.AddComponent<AudioListener>();
                Debug.Log("✓ Fixed: Created new AudioListener GameObject");
                fixedSomething = true;
            }
        }
        else if (listeners.Length > 1)
        {
            Debug.Log($"Multiple AudioListeners found ({listeners.Length}) - disabling extras...");
            
            // Keep the one on Main Camera if it exists, otherwise keep the first one
            Camera mainCam = Camera.main;
            AudioListener keepThis = null;
            
            if (mainCam != null)
            {
                keepThis = mainCam.GetComponent<AudioListener>();
            }
            
            if (keepThis == null)
            {
                keepThis = listeners[0];
            }
            
            foreach (var listener in listeners)
            {
                if (listener != keepThis)
                {
                    listener.enabled = false;
                    Debug.Log($"✓ Fixed: Disabled AudioListener on {listener.gameObject.name}");
                    fixedSomething = true;
                }
            }
        }
        else
        {
            // Exactly one listener - make sure it's enabled
            AudioListener listener = listeners[0];
            if (!listener.enabled)
            {
                listener.enabled = true;
                Debug.Log($"✓ Fixed: Enabled AudioListener on {listener.gameObject.name}");
                fixedSomething = true;
            }
        }
        
        if (!fixedSomething)
        {
            Debug.Log("✓ No issues found - everything looks good!");
        }
        
        Debug.Log("================================================");
        
        // Run diagnostic to show current state
        RunDiagnostic();
    }
    
    [ContextMenu("Test Audio (Beep)")]
    public void TestAudioBeep()
    {
        // Create a simple beep to test if audio is working at all
        GameObject testObj = new GameObject("AudioTest");
        AudioSource testSource = testObj.AddComponent<AudioSource>();
        
        // Generate a simple beep tone
        int sampleRate = 44100;
        float frequency = 440f; // A note
        float duration = 0.5f;
        
        AudioClip beep = AudioClip.Create("Beep", (int)(sampleRate * duration), 1, sampleRate, false);
        float[] samples = new float[(int)(sampleRate * duration)];
        
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * 0.5f;
        }
        
        beep.SetData(samples, 0);
        
        testSource.clip = beep;
        testSource.volume = 1f;
        testSource.spatialBlend = 0f;
        testSource.Play();
        
        Destroy(testObj, duration + 0.1f);
        
        Debug.Log("[AudioTest] Playing test beep (440Hz for 0.5s)");
        Debug.Log("[AudioTest] If you can't hear this, the problem is NOT with your game audio files");
        Debug.Log("[AudioTest] Check: system volume, headphones, speaker connection");
    }
}

