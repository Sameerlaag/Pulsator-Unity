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
    public Transform leftShip;   // Assign Ship GameObject
    public Transform centerShip; // Assign Ship GameObject
    public Transform rightShip;  // Assign Ship GameObject
    private Transform defaultLeftShip;   // Assign Ship GameObject
    private Transform defaultCenterShip; // Assign Ship GameObject
    private Transform defaultRightShip;  // Assign Ship GameObject
    public Transform playerShip;  // Assign Ship GameObject

    [Header("Prefabs")]
    public GameObject yellowCubePrefab; // Standard (Shoot)
    public GameObject redCubePrefab;  // Heavy (Dodge)

    [Header("Game Settings")]
    [Tooltip("How many seconds it takes for a cube to fly from Ship to Player")]
    public float noteTravelTime = 2.0f; 
    public float laneWidth = 2.5f; // Distance between lanes

    private List<EMapNote> notes;
    private int nextNoteIndex = 0;
    private bool isPlaying = false;
    private float positionXRef;
    public float moveSpeed = 50f;
    // Mapping lanes to X coordinates
    // Assuming Lane 2 is center (0), Lane 0 is far left (-4), Lane 4 is far right (+4)
    private float[] laneXPositions; 
    private Transform _playerShip;  // Assign Ship GameObject

    private void Start()
    {
        // Calculate X positions for 5 lanes centered at 0
        laneXPositions = new float[5];
        _playerShip =  playerShip;
        positionXRef = _playerShip.position.x;
        defaultLeftShip = leftShip; 
        defaultCenterShip = centerShip; 
        defaultRightShip = rightShip; 
        for(int i=0; i<5; i++)
        {
            laneXPositions[i] = positionXRef + ((i - 2) * laneWidth);
        }

        // Wait for generator to finish
        mapGenerator.OnMapGenerationComplete += OnMapReady;
    }

    private void OnMapReady()
    {
        notes = mapGenerator.mapNotes;
        // Sort just in case parallel processing messed up order (though linear loop shouldn't)
        notes.Sort((a, b) => a.time.CompareTo(b.time)); 
        
        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        // Optional: Wait for loading screen to fade
        yield return new WaitForSeconds(1.0f);

        musicSource.Play();
        isPlaying = true;
    }

    private void Update()
    {
        if (!isPlaying) return;

        // The current time of the song
        float songTime = musicSource.time;

        // Look ahead! We want to spawn notes that are due in 'noteTravelTime' seconds
        // so they arrive at the player exactly on beat.
        float spawnTime = songTime + noteTravelTime;

        while (nextNoteIndex < notes.Count && notes[nextNoteIndex].time < spawnTime)
        {
            SpawnNote(notes[nextNoteIndex]);
            nextNoteIndex++;
        }
    }

    private void SpawnNote(EMapNote note)
    {
        // 1. Determine which ship fires
        Transform sourceShip = centerShip;
        if (note.lane == 0 || note.lane == 1) sourceShip = leftShip;
        if (note.lane == 3 || note.lane == 4) sourceShip = rightShip;

        // 2. Determine Prefab based on Type
        GameObject prefabToSpawn = (note.type == NoteType.Heavy) ? redCubePrefab : yellowCubePrefab;

        // 3. Spawn
        GameObject cube = Instantiate(prefabToSpawn, sourceShip.position, Quaternion.identity);

        // 4. Setup Cube Movement
        // We calculate exact start and end positions
        Vector3 startPos = sourceShip.position;
        // Target Z is usually 0 or wherever the player is
        Vector3 endPos = new Vector3(laneXPositions[note.lane], _playerShip.position.y, _playerShip.position.z); 

        // We attach a mover script (see below)
        NoteMover mover = cube.AddComponent<NoteMover>();
        sourceShip.position = Vector3.Lerp(sourceShip.position, new Vector3(laneXPositions[note.lane], sourceShip.position.y, sourceShip.position.z), moveSpeed * Time.fixedDeltaTime);

        mover.Initialize(startPos, endPos, noteTravelTime);
        
        // Optional: Trigger Ship Animation here
        // sourceShip.GetComponent<Animator>().SetTrigger("Fire");
    }
}