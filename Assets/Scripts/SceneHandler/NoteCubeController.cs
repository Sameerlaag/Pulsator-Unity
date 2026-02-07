using System.Collections.Generic;
using UnityEngine;

namespace SceneHandler
{
    public class NoteCubeController : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private MeshRenderer cubeRenderer;
        [SerializeField] private Material absorbMaterial;
        [SerializeField] private Material shootMaterial;
        [SerializeField] private Material shieldMaterial;
        [SerializeField] private AudioSource noteAudio;
        public AudioClip[] clipMap;
    
        private RhythmMapData.RhythmNote noteData;
        private float travelSpeed;
        private Vector3 targetPosition;
        private RhythmGameManager gameManager;
        private bool isInitialized = false;
    
        // State
        private bool isDestroyed = false;
    
        public void Initialize(RhythmMapData.RhythmNote data, float speed, Vector3 target, RhythmGameManager manager)
        {
            noteData = data;
            travelSpeed = speed;
            targetPosition = target;
            gameManager = manager;
            isInitialized = true;
        
            // Set visual based on note type
            SetVisualForType(data.type);
        
            Debug.Log($"[NoteCube] Initialized {data.type} note traveling to {target}");
        }
    
        void SetVisualForType(RhythmMapData.RhythmNote.NoteType type)
        {
            if (cubeRenderer == null)
            {
                cubeRenderer = GetComponent<MeshRenderer>();
            }
        
            if (cubeRenderer != null)
            {
                Material mat = type switch
                {
                    RhythmMapData.RhythmNote.NoteType.Absorb => absorbMaterial,
                    RhythmMapData.RhythmNote.NoteType.Shoot => shootMaterial,
                    RhythmMapData.RhythmNote.NoteType.Shield => shieldMaterial,
                    _ => null
                };
            
                if (mat != null)
                {
                    cubeRenderer.material = mat;
                }
                else
                {
                    // Fallback: color-code if no materials assigned
                    Color color = type switch
                    {
                        RhythmMapData.RhythmNote.NoteType.Absorb => Color.cyan,
                        RhythmMapData.RhythmNote.NoteType.Shoot => Color.red,
                        RhythmMapData.RhythmNote.NoteType.Shield => Color.yellow,
                        _ => Color.white
                    };
                    cubeRenderer.material.color = color;
                }
            }
        }
    
        void Update()
        {
            if (!isInitialized || isDestroyed) return;
        
            // Move towards player
            transform.position = Vector3.MoveTowards(
                transform.position, 
                targetPosition, 
                travelSpeed * Time.deltaTime
            );
        
            // Optional: Add rotation for visual flair
            transform.Rotate(Vector3.up * 90f * Time.deltaTime);
        
            // Check if we've reached the player
            if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
            {
                OnReachedPlayer();
            }
        }
    
        void OnReachedPlayer()
        {
            // TODO: This is where we'll check if player performed the right action
            // For now, just destroy
            Debug.Log($"[NoteCube] {noteData.type} note reached player!");
        
            // TODO: Send event to game manager about miss/hit
            DestroyNote();
        }
    
        public void OnPlayerHit(bool wasCorrectAction)
        {
            if (isDestroyed) return;
        
            if (wasCorrectAction)
            {
                Debug.Log($"[NoteCube] HIT! Player correctly handled {noteData.type}");
                // TODO: Add hit effect, score, combo
            }
            else
            {
                Debug.Log($"[NoteCube] MISS! Wrong action for {noteData.type}");
                // TODO: Add miss effect, break combo
            }
        
            DestroyNote();
        }
    
        void DestroyNote()
        {
            if (isDestroyed) return;
        
            isDestroyed = true;
            gameManager?.OnNoteDestroyed(gameObject);
        
            // TODO: Add destruction effect/particle
            if(noteAudio!=null) noteAudio.PlayOneShot(clipMap[(int) noteData.type]);
            Destroy(gameObject);
        }
    
        // Public getters
        public RhythmMapData.RhythmNote.NoteType GetNoteType() => noteData.type;
        public int GetLane() => noteData.lane;
    }
}
