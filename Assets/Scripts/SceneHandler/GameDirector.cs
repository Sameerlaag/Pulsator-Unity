using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class RhythmGameDirector : MonoBehaviour
{
   [Header("References")]
    public AudioMapGenerator mapGenerator;
    public AudioSource musicSource;

    [Header("Ships")]
    public Transform leftShip;
    public Transform centerShip;
    public Transform rightShip;
    public Transform playerShip;

    [Header("Prefabs")]
    public GameObject yellowCubePrefab; // Standard (Shoot)
    public GameObject redCubePrefab;    // Heavy (Dodge)

    [Header("Game Settings")]
    [Tooltip("How many seconds it takes for a cube to fly from Ship to Player")]
    public float noteTravelTime = 2.0f;
    [Tooltip("Distance between lanes")]
    public float laneWidth = 2.5f;
    [Tooltip("How fast ships move to their target lane position")]
    public float shipMoveSpeed = 8f;
    [Tooltip("Optional delay before starting music")]
    public float startDelay = 1f;
    public PlayerLocomotion playerLocomotion; 
    [Header("Audio")]
    public LaneHitSoundController hitSoundController;
    [Header("Debug")]
    public bool autoStartOnMapReady = true;

    private List<EMapNote> notes;
    private int nextNoteIndex = 0;
    private bool isPlaying = false;
    
    // Lane positions and ship targets
    private float[] laneXPositions;
    private Vector3 defaultLeftPos;
    private Vector3 defaultCenterPos;
    private Vector3 defaultRightPos;
    
    // Ship target tracking
    private float leftShipTargetX;
    private float centerShipTargetX;
    private float rightShipTargetX;

    private void Start()
    {
        // Store default ship positions
        defaultLeftPos = leftShip.position;
        defaultCenterPos = centerShip.position;
        defaultRightPos = rightShip.position;

        // Calculate lane X positions (centered around player)
        laneXPositions = new float[mapGenerator.lanes];
        float playerX = playerShip.position.x;
        
        for (int i = 0; i < laneXPositions.Length; i++)
        {
            laneXPositions[i] = playerX + ((i - 2) * laneWidth);
        }

        // Initialize ship targets to their default positions
        leftShipTargetX = leftShip.position.x;
        centerShipTargetX = centerShip.position.x;
        rightShipTargetX = rightShip.position.x;

        // Wait for map generation
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerationComplete += OnMapReady;
        }
        else
        {
            Debug.LogError("[Director] No BeatSyncedMapGenerator assigned!");
        }
    }


    private void OnMapReady()
    {
        notes = mapGenerator.mapNotes;
        notes.Sort((a, b) => a.time.CompareTo(b.time));
        if (playerLocomotion != null)
        {
            playerLocomotion.allLaneXPositions = laneXPositions;
        
            // IMPORTANT: Set the player's initial position to the first lane's official X position
            Vector3 initialPos = playerLocomotion.transform.position;
            initialPos.x = laneXPositions[playerLocomotion.currentLane];
            playerLocomotion.transform.position = initialPos;
        }
    
        Debug.Log($"[Director] Map ready with {notes.Count} notes. Lanes: {mapGenerator.lanes}");
        
        if (autoStartOnMapReady)
        {
            StartCoroutine(StartGameRoutine());
        }
    }

    private IEnumerator StartGameRoutine()
    {
        // Ensure all ship movement is complete before the first note (if necessary)
        // The Update() loop handles ship movement, but we can reset targets here.
        ResetShips(); 
    
        // Give the user/system time to prepare
        yield return new WaitForSeconds(startDelay); 
    
        musicSource.Play();
        isPlaying = true;
        nextNoteIndex = 0;
    
        Debug.Log("[Director] Game started!");
    }

    [ContextMenu("Manual Start")]
    public void ManualStart()
    {
        if (notes == null || notes.Count == 0)
        {
            Debug.LogError("[Director] No map loaded!");
            return;
        }
        StartCoroutine(StartGameRoutine());
    }

    private void Update()
    {
        if (!isPlaying) return;

        // === SAMPLE-BASED TIMING (PERFECT SYNC) ===
        float currentTime = musicSource.timeSamples / (float)musicSource.clip.frequency;
        float spawnTime = currentTime + noteTravelTime;

        // Spawn all notes that should appear now
        while (nextNoteIndex < notes.Count && notes[nextNoteIndex].time <= spawnTime)
        {
            SpawnNote(notes[nextNoteIndex]);
            nextNoteIndex++;
        }

        // Smoothly move ships to their target X positions
        UpdateShipPositions();

        // Stop when finished
        if (nextNoteIndex >= notes.Count && !musicSource.isPlaying)
        {
            isPlaying = false;
            Debug.Log("[Director] Song finished!");
        }
    }

    private void SpawnNote(EMapNote note)
    {
        Transform sourceShip;
        int targetLane = note.lane;

        // === DETERMINISTICALLY PICK A SHIP BASED ON LANE ===
        if (targetLane <= 1)
        {
            // Lanes 0, 1 -> Left Ship
            sourceShip = leftShip;
        }
        else if (targetLane == 2)
        {
            // Lane 2 -> Center Ship
            sourceShip = centerShip;
        }
        else // (targetLane >= 3)
        {
            // Lanes 3, 4 -> Right Ship
            sourceShip = rightShip;
        }

        float targetX = laneXPositions[targetLane];

        // === UPDATE SHIP TARGET ===
        // This part remains, but is now controlled by the deterministic logic
        if (sourceShip == leftShip) 
        {
            leftShipTargetX = targetX;
        }
        else if (sourceShip == centerShip) 
        {
            centerShipTargetX = targetX;
        }
        else if (sourceShip == rightShip) 
        {
            rightShipTargetX = targetX;
        }

        // === CHOOSE PREFAB ===
        GameObject prefab = (note.type == NoteType.Heavy) ? redCubePrefab : yellowCubePrefab;

        // === SPAWN CUBE ===
        GameObject cube = Instantiate(prefab, sourceShip.position, Quaternion.identity);

        Vector3 startPos = sourceShip.position;
        Vector3 endPos = new Vector3(targetX, playerShip.position.y, playerShip.position.z);

        NoteMover mover = cube.GetComponent<NoteMover>();
        if (mover == null)
            mover = cube.AddComponent<NoteMover>();

        mover.Initialize(startPos, endPos, noteTravelTime);

        NoteData data = cube.AddComponent<NoteData>();
        data.noteType = note.type;
        data.lane = note.lane;
        data.power = note.power;
        
        EnemyCollision collisionHandler = cube.GetComponent<EnemyCollision>();
        if (collisionHandler != null)
        {
            collisionHandler.hitController = hitSoundController; // Pass the reference
        }
    }


    private void UpdateShipPositions()
    {
        // Smoothly interpolate ships to their target X positions
        Vector3 leftPos = leftShip.position;
        leftPos.x = Mathf.Lerp(leftPos.x, leftShipTargetX, shipMoveSpeed * Time.deltaTime);
        leftShip.position = leftPos;

        Vector3 centerPos = centerShip.position;
        centerPos.x = Mathf.Lerp(centerPos.x, centerShipTargetX, shipMoveSpeed * Time.deltaTime);
        centerShip.position = centerPos;

        Vector3 rightPos = rightShip.position;
        rightPos.x = Mathf.Lerp(rightPos.x, rightShipTargetX, shipMoveSpeed * Time.deltaTime);
        rightShip.position = rightPos;
    }

    [ContextMenu("Reset Ships")]
    public void ResetShips()
    {
        leftShip.position = defaultLeftPos;
        centerShip.position = defaultCenterPos;
        rightShip.position = defaultRightPos;
        
        leftShipTargetX = defaultLeftPos.x;
        centerShipTargetX = defaultCenterPos.x;
        rightShipTargetX = defaultRightPos.x;
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
            mapGenerator.OnMapGenerationComplete -= OnMapReady;
    }

    // === DEBUG HELPERS ===
    private void OnDrawGizmosSelected()
    {
        if (playerShip == null || laneXPositions == null) return;

        // Draw lane positions
        Gizmos.color = Color.cyan;
        for (int i = 0; i < laneXPositions.Length; i++)
        {
            Vector3 start = new Vector3(laneXPositions[i], playerShip.position.y - 1f, playerShip.position.z);
            Vector3 end = new Vector3(laneXPositions[i], playerShip.position.y + 1f, playerShip.position.z);
            Gizmos.DrawLine(start, end);
        }
    }
}