using UnityEditor;
using UnityEngine;

namespace SceneHandler
{

    /// <summary>
    /// Helper class to create and edit rhythm maps
    /// This is a MonoBehaviour so you can attach it to a GameObject in the scene
    /// and use it to build maps visually
    /// </summary>
    public class RhythmMapEditor : MonoBehaviour
    {
        [Header("Map Being Edited")]
        public RhythmMapData targetMap;
    
        [Header("Quick Add Note")]
        public float timestamp = 0f;
        public int lane = 0;
        public RhythmMapData.RhythmNote.NoteType noteType = RhythmMapData.RhythmNote.NoteType.Absorb;
    
        [Header("Pattern Generation")]
        public float patternStartTime = 0f;
        public float beatInterval = 0.5f;  // Time between notes
        public int patternLength = 4;
        public bool alternatingLanes = true;
    
        // Call this from inspector or custom editor
        public void AddNote()
        {
            if (targetMap == null)
            {
                Debug.LogError("[MapEditor] No target map assigned!");
                return;
            }
        
            RhythmMapData.RhythmNote newNote = new RhythmMapData.RhythmNote
            {
                timestamp = timestamp,
                lane = Mathf.Clamp(lane, 0, 2),
                type = noteType
            };
        
            targetMap.notes.Add(newNote);
        
#if UNITY_EDITOR
            EditorUtility.SetDirty(targetMap);
#endif
        
            Debug.Log($"[MapEditor] Added {noteType} note at {timestamp}s in lane {lane}");
        }
    
        public void GeneratePattern()
        {
            if (targetMap == null)
            {
                Debug.LogError("[MapEditor] No target map assigned!");
                return;
            }
        
            for (int i = 0; i < patternLength; i++)
            {
                float time = patternStartTime + (i * beatInterval);
                int noteLane = alternatingLanes ? (i % 3) : lane;
            
                RhythmMapData.RhythmNote newNote = new RhythmMapData.RhythmNote
                {
                    timestamp = time,
                    lane = noteLane,
                    type = noteType
                };
            
                targetMap.notes.Add(newNote);
            }
        
#if UNITY_EDITOR
            EditorUtility.SetDirty(targetMap);
#endif
        
            Debug.Log($"[MapEditor] Generated {patternLength} note pattern starting at {patternStartTime}s");
        }
    
        public void ClearAllNotes()
        {
            if (targetMap == null) return;
        
            targetMap.notes.Clear();
        
#if UNITY_EDITOR
            EditorUtility.SetDirty(targetMap);
#endif
        
            Debug.Log("[MapEditor] Cleared all notes");
        }
    
        public void SortNotes()
        {
            if (targetMap == null) return;
        
            targetMap.notes.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
        
#if UNITY_EDITOR
            EditorUtility.SetDirty(targetMap);
#endif
        
            Debug.Log("[MapEditor] Sorted notes by timestamp");
        }
    
        // Helper to generate a simple test map
        public void GenerateTestMap()
        {
            if (targetMap == null) return;
        
            targetMap.notes.Clear();
        
            // Create a simple pattern: one note per second alternating lanes
            for (int i = 0; i < 10; i++)
            {
                RhythmMapData.RhythmNote.NoteType type = (RhythmMapData.RhythmNote.NoteType)(i % 3);
            
                targetMap.notes.Add(new RhythmMapData.RhythmNote
                {
                    timestamp = 2f + i * 1f,  // Start at 2 seconds
                    lane = i % 3,
                    type = type
                });
            }
        
#if UNITY_EDITOR
            EditorUtility.SetDirty(targetMap);
#endif
        
            Debug.Log("[MapEditor] Generated test map with 10 notes");
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(RhythmMapEditor))]
    public class RhythmMapEditorInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        
            RhythmMapEditor editor = (RhythmMapEditor)target;
        
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
            if (GUILayout.Button("Add Note"))
            {
                editor.AddNote();
            }
        
            if (GUILayout.Button("Generate Pattern"))
            {
                editor.GeneratePattern();
            }
        
            EditorGUILayout.Space();
        
            if (GUILayout.Button("Generate Test Map"))
            {
                editor.GenerateTestMap();
            }
        
            if (GUILayout.Button("Sort Notes"))
            {
                editor.SortNotes();
            }
        
            EditorGUILayout.Space();
        
            GUI.color = Color.red;
            if (GUILayout.Button("Clear All Notes"))
            {
                if (EditorUtility.DisplayDialog("Clear Notes", 
                        "Are you sure you want to delete all notes?", "Yes", "Cancel"))
                {
                    editor.ClearAllNotes();
                }
            }
            GUI.color = Color.white;
        
            // Display note count
            if (editor.targetMap != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Current note count: {editor.targetMap.notes.Count}", MessageType.Info);
            }
        }
    }
#endif
}