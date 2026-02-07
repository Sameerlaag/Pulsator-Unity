using UnityEngine;

namespace SceneHandler
{
    using UnityEngine;

    /// <summary>
    /// UFO that rapidly moves between lanes to shoot notes
    /// Each UFO has a role and moves to the correct lane when needed
    /// </summary>
    public class UFOController : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private LaneSystem laneSystem;

        [Header("UFO Role")] [Tooltip("What type of notes this UFO shoots")] [SerializeField]
        private RhythmMapData.RhythmNote.NoteType role = RhythmMapData.RhythmNote.NoteType.Absorb;

        [Header("Movement Settings")] [SerializeField]
        private float moveSpeed = 15f;

        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Visual Feedback")] [SerializeField]
        private MeshRenderer ufoRenderer;

        [SerializeField] private ParticleSystem shootEffect;
        [SerializeField] private Material idleMaterial;
        [SerializeField] private Material shootingMaterial;

        [Header("Debug")] [SerializeField] private int currentLane = 0;
        [SerializeField] private bool isMoving = false;

        // Movement state
        private int targetLane = 0;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float moveProgress = 0f;

        // Visual state
        private float shootEffectTimer = 0f;
        private const float SHOOT_EFFECT_DURATION = 0.3f;

        void Start()
        {
            if (laneSystem == null)
            {
                laneSystem = FindObjectOfType<LaneSystem>();
            }

            if (ufoRenderer == null)
            {
                ufoRenderer = GetComponent<MeshRenderer>();
            }

            // Start at lane 0
            SnapToLane(0);

            Debug.Log($"[UFO] {gameObject.name} initialized with role: {role}");
        }

        void Update()
        {
            // Update movement
            if (isMoving)
            {
                UpdateMovement();
            }

            // Update visual effects
            if (shootEffectTimer > 0)
            {
                shootEffectTimer -= Time.deltaTime;
                if (shootEffectTimer <= 0 && ufoRenderer != null && idleMaterial != null)
                {
                    ufoRenderer.material = idleMaterial;
                }
            }
        }

        /// <summary>
        /// Move to a lane and shoot a note
        /// </summary>
        public void MoveAndShoot(int lane, RhythmMapData.RhythmNote.NoteType noteType)
        {
            // Validate note type matches our role
            if (noteType != role)
            {
                Debug.LogWarning($"[UFO] {gameObject.name} (role: {role}) asked to shoot {noteType}!");
            }

            // Move to lane if not already there
            if (lane != currentLane || isMoving)
            {
                MoveToLane(lane);
            }

            // Shoot visual feedback
            PlayShootEffect();
        }

        void MoveToLane(int lane)
        {
            // Clamp lane
            lane = Mathf.Clamp(lane, 0, laneSystem.LaneCount - 1);

            // If already at target and not moving, no need to move
            if (lane == currentLane && !isMoving)
            {
                return;
            }

            targetLane = lane;
            startPosition = transform.position;
            targetPosition = laneSystem.GetLaneSpawnPosition(lane);

            moveProgress = 0f;
            isMoving = true;

            Debug.Log($"[UFO] {gameObject.name} moving from lane {currentLane} to {targetLane}");
        }

        void UpdateMovement()
        {
            float distance = Vector3.Distance(startPosition, targetPosition);
            moveProgress += (moveSpeed / distance) * Time.deltaTime;
            moveProgress = Mathf.Clamp01(moveProgress);

            float curvedProgress = moveCurve.Evaluate(moveProgress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, curvedProgress);

            if (moveProgress >= 1f)
            {
                transform.position = targetPosition;
                currentLane = targetLane;
                isMoving = false;

                Debug.Log($"[UFO] {gameObject.name} arrived at lane {currentLane}");
            }
        }

        void SnapToLane(int lane)
        {
            lane = Mathf.Clamp(lane, 0, laneSystem.LaneCount - 1);
            currentLane = lane;
            targetLane = lane;
            transform.position = laneSystem.GetLaneSpawnPosition(lane);
            isMoving = false;
            moveProgress = 0f;
        }

        void PlayShootEffect()
        {
            Debug.Log($"[UFO] {gameObject.name} SHOOTING {role} note in lane {currentLane}");

            // Material flash
            if (ufoRenderer != null && shootingMaterial != null)
            {
                ufoRenderer.material = shootingMaterial;
                shootEffectTimer = SHOOT_EFFECT_DURATION;
            }

            // Particle effect
            if (shootEffect != null)
            {
                shootEffect.Play();
            }

            // TODO: Add sound effect
            // TODO: Add animation
        }

        // Public getters
        public RhythmMapData.RhythmNote.NoteType GetRole() => role;
        public int GetCurrentLane() => currentLane;
        public int GetTargetLane() => targetLane;
        public bool IsMoving() => isMoving;

        // Visualize in editor
        void OnDrawGizmos()
        {
            if (laneSystem == null) return;

            Gizmos.color = role switch
            {
                RhythmMapData.RhythmNote.NoteType.Absorb => Color.cyan,
                RhythmMapData.RhythmNote.NoteType.Shoot => Color.red,
                RhythmMapData.RhythmNote.NoteType.Shield => Color.yellow,
                _ => Color.white
            };

            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw line to current lane
            if (Application.isPlaying && !isMoving)
            {
                Vector3 lanePos = laneSystem.GetLaneSpawnPosition(currentLane);
                Gizmos.DrawLine(transform.position, lanePos);
            }
        }
    }
}