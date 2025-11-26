using System.Collections.Generic;
using UnityEngine;

public class BeatSyncedNoteSpawner : MonoBehaviour
{
    [Header("References")]
    public AudioSource audioSource;
    public AudioMapGenerator mapGenerator;
    
    [Header("Spawn Settings")]
    public GameObject standardNotePrefab; // Blue cube
    public GameObject heavyNotePrefab;    // Red cube
    [Tooltip("Distance from player where notes spawn")]
    public float spawnDistance = 50f;
    [Tooltip("How many seconds ahead to spawn notes (travel time)")]
    public float anticipationTime = 3f;
    [Tooltip("Z position of the player target line")]
    public float playerZ = 0f;
    
    [Header("Lane Positions")]
    public float laneSpacing = 2f;
    public float laneYPosition = 0f;

    private List<EMapNote> mapNotes;
    private int nextNoteIndex = 0;
    private bool isPlaying = false;

    private void Start()
    {
        // Wait for map to be ready
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerationComplete += OnMapReady;
        }
    }

    private void OnMapReady()
    {
        mapNotes = mapGenerator.mapNotes;
        Debug.Log($"[Spawner] Map ready with {mapNotes.Count} notes");
        
        // Auto-start if desired
        // StartPlayback();
    }

    [ContextMenu("Start Playback")]
    public void StartPlayback()
    {
        if (mapNotes == null || mapNotes.Count == 0)
        {
            Debug.LogError("[Spawner] No map loaded!");
            return;
        }

        nextNoteIndex = 0;
        isPlaying = true;
        audioSource.Play();
        Debug.Log("[Spawner] Playback started!");
    }

    [ContextMenu("Stop Playback")]
    public void StopPlayback()
    {
        isPlaying = false;
        audioSource.Stop();
        nextNoteIndex = 0;
    }

    private void Update()
    {
        if (!isPlaying || mapNotes == null || nextNoteIndex >= mapNotes.Count)
            return;

        // === SAMPLE-BASED TIMING (PERFECT SYNC) ===
        float currentTime = audioSource.timeSamples / (float)audioSource.clip.frequency;
        float spawnTime = currentTime + anticipationTime;

        // Spawn all notes that should appear now
        while (nextNoteIndex < mapNotes.Count && mapNotes[nextNoteIndex].time <= spawnTime)
        {
            SpawnNote(mapNotes[nextNoteIndex]);
            nextNoteIndex++;
        }

        // Stop when song ends
        if (nextNoteIndex >= mapNotes.Count && !audioSource.isPlaying)
        {
            isPlaying = false;
            Debug.Log("[Spawner] Song finished!");
        }
    }

    private void SpawnNote(EMapNote note)
    {
        // Calculate lane position (centered around 0)
        float xPos = (note.lane - (mapGenerator.lanes / 2f)) * laneSpacing;
        Vector3 startPos = new Vector3(xPos, laneYPosition, playerZ + spawnDistance);
        Vector3 endPos = new Vector3(xPos, laneYPosition, playerZ);

        // Choose prefab based on type
        GameObject prefab = note.type == NoteType.Heavy ? heavyNotePrefab : standardNotePrefab;
        
        GameObject noteObj = Instantiate(prefab, startPos, Quaternion.identity);
        
        // Apply color coding
        Renderer rend = noteObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = note.type == NoteType.Heavy ? Color.red : Color.cyan;
        }

        // Initialize movement
        NoteMover mover = noteObj.GetComponent<NoteMover>();
        if (mover == null)
            mover = noteObj.AddComponent<NoteMover>();
        
        mover.Initialize(startPos, endPos, anticipationTime);

        // Optional: Store note data for gameplay logic
        NoteData data = noteObj.AddComponent<NoteData>();
        data.noteType = note.type;
        data.lane = note.lane;
        data.power = note.power;
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
            mapGenerator.OnMapGenerationComplete -= OnMapReady;
    }
}

// Attach this to note GameObjects to store their data
public class NoteData : MonoBehaviour
{
    public NoteType noteType;
    public int lane;
    public float power;
}