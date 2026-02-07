using System.Collections;
using System.Collections.Generic;
using PlayerManager;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SceneHandler
{
    /// <summary>
    /// Updated Game Manager for 5-lane system with dynamic UFOs
    /// </summary>
    public class RhythmGameManager : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private RhythmMapData currentMap;

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private LaneSystem laneSystem;
        [SerializeField] private PlayerController playerController;

        [Header("Manual Overrides")]
        [Tooltip("Override the song from the map (leave empty to use map's song)")]
        [SerializeField]
        private AudioClip manualSongOverride;

        [Header("UFO Configuration")] [Tooltip("3 UFOs, one for each note type")] [SerializeField]
        private UFOController absorbUFO;

        [SerializeField] private UFOController shootUFO;
        [SerializeField] private UFOController shieldUFO;

        [Header("Prefabs")] [SerializeField] private GameObject noteCubePrefab;

        private bool isPlaying = false;

        [SerializeField] private float songTime = 0f;
        [SerializeField] private int notesSpawned = 0;
        [SerializeField] private int notesRemaining = 0;

        private Queue<RhythmMapData.RhythmNote> upcomingNotes = new Queue<RhythmMapData.RhythmNote>();
        private List<GameObject> activeNotes = new List<GameObject>();
        private bool isReady = false;
        float sessionTime = 0f;
        float leadInTime;
        bool musicStarted = false;

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

            // Determine which audio clip to use
            AudioClip clipToUse = manualSongOverride != null ? manualSongOverride : currentMap.musicTrack;

            if (clipToUse == null)
            {
                Debug.LogError("[GameManager] ❌ No audio clip available!");
                Debug.LogError(
                    "[GameManager] Either assign 'Manual Song Override' OR make sure your RhythmMapData has a music track");
                yield break;
            }

            // Detailed audio logging
            Debug.Log("========== AUDIO SETUP ==========");
            Debug.Log($"[GameManager] Source: {(manualSongOverride != null ? "Manual Override ✓" : "Map Track")}");
            Debug.Log($"[GameManager] Clip: {clipToUse.name}");
            Debug.Log($"[GameManager] Length: {clipToUse.length:F2} seconds");
            Debug.Log($"[GameManager] Channels: {clipToUse.channels}");
            Debug.Log($"[GameManager] Frequency: {clipToUse.frequency}Hz");
            Debug.Log($"[GameManager] Load State: {clipToUse.loadState}");
            Debug.Log($"[GameManager] Samples: {clipToUse.samples}");

            if (clipToUse.length == 0)
            {
                Debug.LogError("[GameManager] ❌ CLIP LENGTH IS ZERO! File may be corrupted!");
            }

            if (clipToUse.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogWarning($"[GameManager] ⚠️ Clip not loaded! State: {clipToUse.loadState}");
            }

            // Configure AudioSource
            musicSource.clip = clipToUse;
            musicSource.playOnAwake = false;
            musicSource.volume = 1f;
            musicSource.spatialBlend = 0f; // 2D audio (important for music!)
            musicSource.mute = false;
            musicSource.loop = false;

            Debug.Log($"[GameManager] AudioSource configured:");
            Debug.Log($"  Volume: {musicSource.volume}");
            Debug.Log($"  Muted: {musicSource.mute}");
            Debug.Log($"  Spatial Blend: {musicSource.spatialBlend} (0=2D, 1=3D)");
            Debug.Log("=================================");

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
                sessionTime += Time.deltaTime;

                if (!musicStarted)
                {
                    double dspStartTime = AudioSettings.dspTime + leadInTime;
                    musicSource.PlayScheduled(dspStartTime);
                    musicStarted = true;
                    Debug.Log("🎵 Music scheduled");
                }

                songTime = sessionTime - leadInTime;

                while (upcomingNotes.Count > 0)
                {
                    var nextNote = upcomingNotes.Peek();
                    float spawnTime = CalculateSpawnTime(nextNote);

                    if (songTime >= spawnTime)
                        SpawnNote(upcomingNotes.Dequeue());
                    else
                        break;
                }

                // Check if song is over
                if (songTime > musicSource.clip.length && activeNotes.Count == 0)
                {
                    EndGame();
                }
            }
        }
        double dspStartTime;

        void StartGame()
        {
            leadInTime = GetMaxTravelTime();
            sessionTime = 0f;
            songTime = 0f;

            dspStartTime = AudioSettings.dspTime + leadInTime;
            musicSource.PlayScheduled(dspStartTime);

            musicStarted = true;
            isPlaying = true;
        }


        float GetMaxTravelTime()
        {
            float max = 0f;

            foreach (var note in currentMap.notes)
            {
                float dist = laneSystem.GetSpawnDistance(note.lane);
                float travel = dist / currentMap.noteSpeed;
                if (travel > max)
                    max = travel;
            }

            return max;
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

            Debug.Log($"[GameManager] Spawned {noteData.type} note in lane {noteData.lane} at {songTime:F2}s");
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
            bool valid = true;

            if (currentMap == null)
            {
                Debug.LogError("[GameManager] ❌ No map assigned!");
                valid = false;
            }

            // Only check map's music track if we don't have a manual override
            if (manualSongOverride == null && (currentMap == null || currentMap.musicTrack == null))
            {
                Debug.LogError("[GameManager] ❌ No audio! Need either 'Manual Song Override' OR map with music track!");
                valid = false;
            }

            if (laneSystem == null)
            {
                Debug.LogError("[GameManager] ❌ No lane system!");
                valid = false;
            }

            if (playerController == null)
            {
                Debug.LogError("[GameManager] ❌ No player controller!");
                valid = false;
            }

            if (noteCubePrefab == null)
            {
                Debug.LogError("[GameManager] ❌ No note cube prefab!");
                valid = false;
            }

            // Validate UFOs
            if (absorbUFO == null || shootUFO == null || shieldUFO == null)
            {
                Debug.LogError("[GameManager] ❌ Not all UFOs assigned!");
                Debug.LogError($"  Absorb UFO: {(absorbUFO != null ? "✓" : "❌")}");
                Debug.LogError($"  Shoot UFO: {(shootUFO != null ? "✓" : "❌")}");
                Debug.LogError($"  Shield UFO: {(shieldUFO != null ? "✓" : "❌")}");
                valid = false;
            }

            return valid;
        }

        // Public getters
        public float GetSongTime() => songTime;
        public int GetActiveNotesCount() => activeNotes.Count;
        public int GetNotesSpawned() => notesSpawned;
        public int GetNotesRemaining() => notesRemaining;
        public PlayerController GetPlayer() => playerController;
    }
}