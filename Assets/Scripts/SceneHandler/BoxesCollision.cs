using UnityEngine;

public class EnemyCollision : MonoBehaviour
{
    public GameObject miniCubePrefab;   // Assign in Inspector
    public int explosionPieces = 10;    // Number of fragments
    public float explosionForce = 3f;   // Push force
    public float pieceLifetime = 1f;    // How long pieces last

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
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