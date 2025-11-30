using UnityEngine;

public class EnemyCollision : MonoBehaviour
{
    public GameObject miniCubePrefab;   // Assign in Inspector
    public int explosionPieces = 10;    // Number of fragments
    public float explosionForce = 3f;   // Push force
    public float pieceLifetime = 1f;    // How long pieces last
    public RhythmGameDirector gameDirector;

    // 1. MUST be set by the Director when the cube is created
    [HideInInspector] public LaneHitSoundController hitController; 

    private NoteData noteData; // Cache the NoteData component for easy access

    private void Start()
    {
        // 2. Get the NoteData component (assuming it was added by the Director)
        noteData = GetComponent<NoteData>();
        if (noteData == null)
        {
            Debug.LogError("[EnemyCollision] NoteData component missing on cube!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // --- AUDIO HANDLING ---
            if (hitController != null && noteData != null)
            {
                // 3. Call PlayHitSound using the lane index from the cube's NoteData
                hitController.PlayHitSound(noteData.lane); 
            }
            // ----------------------
            gameDirector.updateScore(noteData.noteType == NoteType.Heavy ? 50 : 10);
            Explode();
            Destroy(gameObject); 
        }

        if (other.CompareTag("BackWall"))
        {
            Destroy(gameObject);
        }
    }

    void Explode()
    {
        for (int i = 0; i < explosionPieces; i++)
        {
            // Random position near the enemy
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * 0.3f;

            GameObject piece = Instantiate(miniCubePrefab, spawnPos, Random.rotation);

            Rigidbody rb = piece.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Random force
                rb.AddExplosionForce(explosionForce, transform.position, 1f);
            }

            Destroy(piece, pieceLifetime);
        }
    }
}