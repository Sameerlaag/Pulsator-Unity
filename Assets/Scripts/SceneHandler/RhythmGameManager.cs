using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SceneHandler
{
    /// <summary>
    /// Updated Game Manager for 5-lane system with dynamic UFOs
    /// </summary>
    public class RhythmGameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RhythmMapData currentMap;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private LaneSystem laneSystem;
        [SerializeField] private PlayerController playerController;
    
        [Header("UFO Configuration")]
        [Tooltip("3 UFOs, one for each note type")]
        [SerializeField] private UFOController absorbUFO;
        [SerializeField] private UFOController shootUFO;
        [SerializeField] private UFOController shieldUFO;
    
        [Header("Prefabs")]
        [SerializeField] private GameObject noteCubePrefab;
    
        [Header("Runtime State")]
        [SerializeField] private bool isPlaying = false;
        [SerializeField] private float songTime = 0f;
        [SerializeField] private int notesSpawned = 0;
        [SerializeField] private int notesRemaining = 0;
    
        private Queue<RhythmMapData.RhythmNote> upcomingNotes = new Queue<RhythmMapData.RhythmNote>();
        private List<GameObject> activeNotes = new List<GameObject>();
        private bool isReady = false;
    
        void Start()
        {
            StartCoroutine(InitializeGame());
        }
    
        IEnumerator InitializeGame()
        {
            Debug.Log("[GameManager] Initializing improved rhythm game...");
        
            // Validate
            if (!ValidateSetup())
            {
                Debug.LogError("[GameManager] Setup validation failed!");
                yield break;
            }
        
            // Calculate lane system
            laneSystem.CalculateLaneSystem();
        
            // Setup audio
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
            musicSource.clip = currentMap.musicTrack;
            musicSource.playOnAwake = false;
        
            // Load and sort notes
            List<RhythmMapData.RhythmNote> sortedNotes = new List<RhythmMapData.RhythmNote>(currentMap.notes);
            sortedNotes.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
        
            foreach (var note in sortedNotes)
            {
                upcomingNotes.Enqueue(note);
            }
        
            notesRemaining = upcomingNotes.Count;
        
            Debug.Log($"[GameManager] Loaded {notesRemaining} notes");
            Debug.Log($"[GameManager] Lane system ready with {laneSystem.LaneCount} lanes");
        
            yield return new WaitForSeconds(0.5f);
        
            isReady = true;
            Debug.Log("[GameManager] ✓ Ready! Press SPACE to start");
        }
    
        void Update()
        {
            // Start game
            if (isReady && !isPlaying && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartGame();
            }
        
            if (isPlaying)
            {
                songTime += Time.deltaTime;
            
                // Spawn notes
                while (upcomingNotes.Count > 0)
                {
                    RhythmMapData.RhythmNote nextNote = upcomingNotes.Peek();
                    float spawnTime = CalculateSpawnTime(nextNote);
                
                    if (songTime >= spawnTime)
                    {
                        SpawnNote(upcomingNotes.Dequeue());
                    }
                    else
                    {
                        break;
                    }
                }
            
                // Check if song is over
                if (songTime > musicSource.clip.length && activeNotes.Count == 0)
                {
                    EndGame();
                }
            }
        }
    
        void StartGame()
        {
            Debug.Log("[GameManager] Starting game!");
            isPlaying = true;
            songTime = 0f;
            notesSpawned = 0;
            musicSource.Play();
        }
    
        float CalculateSpawnTime(RhythmMapData.RhythmNote note)
        {
            // Get spawn distance for this specific lane
            float spawnDistance = laneSystem.GetSpawnDistance(note.lane);
            float travelTime = spawnDistance / currentMap.noteSpeed;
            return note.timestamp - travelTime;
        }
    
        void SpawnNote(RhythmMapData.RhythmNote noteData)
        {
            // Validate lane
            if (noteData.lane < 0 || noteData.lane >= laneSystem.LaneCount)
            {
                Debug.LogWarning($"[GameManager] Invalid lane {noteData.lane} for note");
                return;
            }
        
            // Get the appropriate UFO for this note type
            UFOController ufo = GetUFOForNoteType(noteData.type);
            if (ufo == null)
            {
                Debug.LogWarning($"[GameManager] No UFO assigned for type {noteData.type}");
                return;
            }
        
            // Move UFO to lane and trigger shoot animation
            ufo.MoveAndShoot(noteData.lane, noteData.type);
        
            // Get spawn position from lane system
            Vector3 spawnPos = laneSystem.GetLaneSpawnPosition(noteData.lane);
        
            // Spawn note cube
            GameObject noteCube = Instantiate(noteCubePrefab, spawnPos, Quaternion.identity);
            activeNotes.Add(noteCube);
        
            // Initialize note
            NoteCubeController cubeController = noteCube.GetComponent<NoteCubeController>();
            if (cubeController != null)
            {
                Vector3 targetPos = laneSystem.PlayerTargetPosition;
                // Adjust target to match lane X
                targetPos.x = laneSystem.GetLaneXPosition(noteData.lane);
            
                cubeController.Initialize(noteData, currentMap.noteSpeed, targetPos, this);
            }
        
            notesSpawned++;
            notesRemaining--;
        
            Debug.Log($"[GameManager] Spawned {noteData.type} note in lane {noteData.lane} (UFO moved from {ufo.GetCurrentLane()})");
        }
    
        UFOController GetUFOForNoteType(RhythmMapData.RhythmNote.NoteType type)
        {
            return type switch
            {
                RhythmMapData.RhythmNote.NoteType.Absorb => absorbUFO,
                RhythmMapData.RhythmNote.NoteType.Shoot => shootUFO,
                RhythmMapData.RhythmNote.NoteType.Shield => shieldUFO,
                _ => null
            };
        }
    
        public void OnNoteDestroyed(GameObject note)
        {
            activeNotes.Remove(note);
        }
    
        void EndGame()
        {
            Debug.Log("[GameManager] Game ended!");
            Debug.Log($"[GameManager] Total notes spawned: {notesSpawned}");
            isPlaying = false;
        }
    
        bool ValidateSetup()
        {
            if (currentMap == null)
            {
                Debug.LogError("[GameManager] No map assigned!");
                return false;
            }
        
            if (currentMap.musicTrack == null)
            {
                Debug.LogError("[GameManager] No music track!");
                return false;
            }
        
            if (laneSystem == null)
            {
                Debug.LogError("[GameManager] No lane system!");
                return false;
            }
        
            if (playerController == null)
            {
                Debug.LogError("[GameManager] No player controller!");
                return false;
            }
        
            if (noteCubePrefab == null)
            {
                Debug.LogError("[GameManager] No note cube prefab!");
                return false;
            }
        
            // Validate UFOs
            if (absorbUFO == null || shootUFO == null || shieldUFO == null)
            {
                Debug.LogError("[GameManager] Not all UFOs assigned!");
                return false;
            }
        
            return true;
        }
    
        // Public getters
        public float GetSongTime() => songTime;
        public int GetActiveNotesCount() => activeNotes.Count;
        public int GetNotesSpawned() => notesSpawned;
        public int GetNotesRemaining() => notesRemaining;
        public PlayerController GetPlayer() => playerController;
    }
}
