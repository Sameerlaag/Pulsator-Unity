using UnityEngine;

public class UFOLaneController : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The unique ID (e.g., 0, 1, 2) that the Director uses to reference this ship.")]
    public int shipID;
    
    [Tooltip("How fast this ship moves to its target lane position")]
    public float moveSpeed = 8f;

    [Header("Prefabs")]
    public GameObject yellowCubePrefab; // Standard (Shoot)
    public GameObject redCubePrefab;    // Heavy (Dodge)

    // References set by the Director
    [HideInInspector] public float noteTravelTime;
    [HideInInspector] public Transform playerShip;
    [HideInInspector] public LaneHitSoundController hitSoundController;
    [HideInInspector] public RhythmGameDirector gameDirector;
    [HideInInspector] public float[] allLaneXPositions; // The X-positions for all 5 possible lanes

    private Vector3 defaultPosition;
    private float targetX;

    void Awake()
    {
        defaultPosition = transform.position;
        targetX = defaultPosition.x;
    }

    void Update()
    {
        // Smoothly interpolate ship to its target X position
        Vector3 currentPos = transform.position;
        currentPos.x = Mathf.Lerp(currentPos.x, targetX, moveSpeed * Time.deltaTime);
        transform.position = currentPos;
    }


    public void ResetShipPosition()
    {
        transform.position = defaultPosition;
        targetX = defaultPosition.x;
    }


    public void SpawnNote(EMapNote note)
    {
        int targetLane = note.lane;
        float targetLaneX = allLaneXPositions[targetLane];
        
        // 1. Set the new movement target for this ship
        targetX = targetLaneX;

        // 2. Choose Prefab
        GameObject prefab = (note.type == NoteType.Heavy) ? redCubePrefab : yellowCubePrefab;

        // 3. Calculate start/end positions
        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(targetLaneX, playerShip.position.y, playerShip.position.z);

        // 4. Instantiate and Initialize
        GameObject cube = Instantiate(prefab, startPos, Quaternion.identity);

        NoteMover mover = cube.GetComponent<NoteMover>();
        if (mover == null)
        {
            mover = cube.AddComponent<NoteMover>();
        }
        mover.Initialize(startPos, endPos, noteTravelTime);

        // 5. Add Data
        NoteData data = cube.AddComponent<NoteData>();
        data.noteType = note.type;
        data.lane = note.lane;
        data.power = note.power;

        // 6. Set up Collision Handling
        EnemyCollision collisionHandler = cube.GetComponent<EnemyCollision>();
        if (collisionHandler != null)
        {
            collisionHandler.hitController = hitSoundController;
            collisionHandler.gameDirector = gameDirector; 
        }
    }
}