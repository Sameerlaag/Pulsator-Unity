using System.Collections;
using System.Collections.Generic;
using PlayerManager;
using UnityEngine;
using UnityEngine.InputSystem;
using UI;

namespace SceneHandler
{
    /// <summary>
    /// Updated RhythmGameManager with BeatmapManager integration
    /// </summary>
    public class RhythmGameManager : MonoBehaviour
    {
        [Header("Manager References")]
        [SerializeField] private MainMenuUIManager menuUIManager;
        [SerializeField] private BeatmapManager beatmapManager;

        [Header("Game References")]
        [SerializeField] private RhythmMapData currentMap;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private LaneSystem laneSystem;
        [SerializeField] private PlayerController playerController;

        [Header("Manual Overrides")]
        [Tooltip("Override the song from the map (leave empty to use map's song)")]
        [SerializeField] private AudioClip manualSongOverride;

        [Header("UFO Configuration")]
        [Tooltip("3 UFOs, one for each note type")]
        [SerializeField] private UFOController absorbUFO;
        [SerializeField] private UFOController shootUFO;
        [SerializeField] private UFOController shieldUFO;

        [Header("Prefabs")]
        [SerializeField] private GameObject noteCubePrefab;

        private bool isPlaying = false;
        [SerializeField] private float songTime = 0f;
        [SerializeField] private int notesSpawned = 0;
        [SerializeField] private int notesRemaining = 0;

        private Queue<RhythmMapData.RhythmNote> upcomingNotes = new Queue<RhythmMapData.RhythmNote>();
        private List<GameObject> activeNotes = new List<GameObject>();
        private bool isReady = false;
        private float sessionTime = 0f;
        private float leadInTime;
        private bool musicStarted = false;

        void Start()
        {
            // Don't auto-start - wait for BeatmapManager and UI
            Debug.Log("[GameManager] Waiting for beatmap initialization...");
        }

        /// <summary>
        /// Called by MainMenuUIManager when a song is selected
        /// </summary>
        public void SetMapAndPrepare(RhythmMapData mapData)
        {
            currentMap = mapData;
            StartCoroutine(InitializeGame());
        }

        IEnumerator InitializeGame()
        {
            Debug.Log("[GameManager] Initializing rhythm game...");

            if (!ValidateSetup())
            {
                Debug.LogError("[GameManager] Setup validation failed!");
                yield break;
            }

            laneSystem.CalculateLaneSystem();

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            AudioClip clipToUse = manualSongOverride != null ? manualSongOverride : currentMap.musicTrack;

            if (clipToUse == null)
            {
                Debug.LogError("[GameManager] No audio clip available!");
                yield break;
            }

            Debug.Log("========== AUDIO SETUP ==========");
            Debug.Log($"[GameManager] Map: {currentMap.mapName}");
            Debug.Log($"[GameManager] Clip: {clipToUse.name}");
            Debug.Log($"[GameManager] Length: {clipToUse.length:F2} seconds");
            Debug.Log($"[GameManager] BPM: {currentMap.bpm:F1}");
            Debug.Log("=================================");

            musicSource.clip = clipToUse;
            musicSource.playOnAwake = false;
            musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
            musicSource.spatialBlend = 0f;
            musicSource.mute = false;
            musicSource.loop = false;

            List<RhythmMapData.RhythmNote> sortedNotes = new List<RhythmMapData.RhythmNote>(currentMap.notes);
            sortedNotes.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));

            upcomingNotes.Clear();
            foreach (var note in sortedNotes)
            {
                upcomingNotes.Enqueue(note);
            }

            notesRemaining = upcomingNotes.Count;

            Debug.Log($"[GameManager] Loaded {notesRemaining} notes");
            Debug.Log($"[GameManager] Lane system ready with {laneSystem.LaneCount} lanes");

            yield return new WaitForSeconds(0.5f);

            isReady = true;
            Debug.Log("[GameManager] ✓ Ready! Waiting for UI start signal...");
        }

        /// <summary>
        /// Called by UI when loading is complete
        /// </summary>
        public void BeginGameFromUI()
        {
            if (isReady && !isPlaying)
            {
                StartGame();
            }
            else
            {
                Debug.LogWarning("[GameManager] Cannot start - game not ready or already playing!");
            }
        }

        void Update()
        {
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

                if (songTime > musicSource.clip.length && activeNotes.Count == 0)
                {
                    EndGame();
                }
            }
        }

        void StartGame()
        {
            leadInTime = GetMaxTravelTime();
            sessionTime = 0f;
            songTime = 0f;
            musicStarted = false;
            isPlaying = true;
            
            Debug.Log("[GameManager] ✓ Game started!");
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
            float spawnDistance = laneSystem.GetSpawnDistance(note.lane);
            float travelTime = spawnDistance / currentMap.noteSpeed;
            return note.timestamp - travelTime;
        }

        void SpawnNote(RhythmMapData.RhythmNote noteData)
        {
            if (noteData.lane < 0 || noteData.lane >= laneSystem.LaneCount)
            {
                Debug.LogWarning($"[GameManager] Invalid lane {noteData.lane} for note");
                return;
            }

            UFOController ufo = GetUFOForNoteType(noteData.type);
            if (ufo == null)
            {
                Debug.LogWarning($"[GameManager] No UFO assigned for type {noteData.type}");
                return;
            }

            ufo.MoveAndShoot(noteData.lane, noteData.type);

            Vector3 spawnPos = laneSystem.GetLaneSpawnPosition(noteData.lane);
            GameObject noteCube = Instantiate(noteCubePrefab, spawnPos, Quaternion.identity);
            activeNotes.Add(noteCube);

            NoteCubeController cubeController = noteCube.GetComponent<NoteCubeController>();
            if (cubeController != null)
            {
                Vector3 targetPos = laneSystem.PlayerTargetPosition;
                targetPos.x = laneSystem.GetLaneXPosition(noteData.lane);
                cubeController.Initialize(noteData, currentMap.noteSpeed, targetPos, this);
            }

            notesSpawned++;
            notesRemaining--;
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

            StartCoroutine(ReturnToMenuAfterDelay(3f));
        }

        IEnumerator ReturnToMenuAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (menuUIManager != null)
            {
                menuUIManager.ShowMenu();
            }

            ResetGameState();
        }

        void ResetGameState()
        {
            isPlaying = false;
            isReady = false;
            songTime = 0f;
            sessionTime = 0f;
            notesSpawned = 0;
            notesRemaining = 0;
            musicStarted = false;

            foreach (var note in activeNotes)
            {
                if (note != null)
                    Destroy(note);
            }
            activeNotes.Clear();

            upcomingNotes.Clear();
        }

        bool ValidateSetup()
        {
            bool valid = true;

            if (currentMap == null)
            {
                Debug.LogError("[GameManager] ✗ No map assigned!");
                valid = false;
            }

            if (manualSongOverride == null && (currentMap == null || currentMap.musicTrack == null))
            {
                Debug.LogError("[GameManager] ✗ No audio!");
                valid = false;
            }

            if (laneSystem == null)
            {
                Debug.LogError("[GameManager] ✗ No lane system!");
                valid = false;
            }

            if (playerController == null)
            {
                Debug.LogError("[GameManager] ✗ No player controller!");
                valid = false;
            }

            if (noteCubePrefab == null)
            {
                Debug.LogError("[GameManager] ✗ No note cube prefab!");
                valid = false;
            }

            if (absorbUFO == null || shootUFO == null || shieldUFO == null)
            {
                Debug.LogError("[GameManager] ✗ Not all UFOs assigned!");
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
        public bool IsReady() => isReady;
    }
}