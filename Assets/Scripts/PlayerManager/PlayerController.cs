using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerManager
{
    /// <summary>
    /// Rhythm-game optimized player controller
    /// - Snappy instant movement for rhythm accuracy
    /// - Input queue system for fast lane switching when mashing
    /// - Action VFX: spinning for shoot, shield bubble that locks movement
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private LaneSystem laneSystem;

        [SerializeField] private Rigidbody rb;
        [SerializeField] private Transform shipModel; // The visual part that rotates
        [SerializeField] private GameObject shieldPrefab; // Shield sphere VFX

        [Header("Movement Settings")] [SerializeField]
        private float baseMoveSpeed = 20f;

        [SerializeField] private float mashBoostMultiplier = 1.8f; // Speed up when mashing same direction
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Input Queue Settings")] [SerializeField]
        private float inputQueueWindow = 0.15f; // Queue inputs during movement

        [SerializeField] private float sameDirectionWindow = 0.25f; // Detect mashing

        [Header("Shoot VFX")] [SerializeField] private float shootSpinSpeed = 2160f; // Degrees/sec (6 rotations/sec)
        [SerializeField] private float shootSpinDuration = 0.12f;

        [Header("Shield VFX")] [SerializeField]
        private float shieldExpandSpeed = 15f;

        [SerializeField] private float shieldShrinkSpeed = 20f;
        [SerializeField] private float shieldMaxScale = 3f;
        [SerializeField] private Color shieldColor = new Color(0.3f, 0.6f, 1f, 0.3f);

        [Header("Current State")] [SerializeField]
        private PlayerAction currentAction = PlayerAction.Neutral;

        [SerializeField] private int currentLane = 2;
        [SerializeField] private bool isMoving = false;
        [SerializeField] private bool movementLocked = false; // Shield locks movement

        [Header("Debug")] [SerializeField] private string lastInputDirection = "";
        [SerializeField] private int consecutiveSameDir = 0;
        [SerializeField] private bool hasQueuedInput = false;

        // Movement state
        private int targetLane = 2;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float moveProgress = 0f;
        private float currentSpeed = 20f;

        // Input tracking
        private string queuedDirection = "";
        private float lastInputTime = 0f;
        private string lastDirection = "";

        // Shoot VFX state
        private bool isShooting = false;
        private float shootTimer = 0f;
        private Quaternion shipOriginalRotation;

        // Shield VFX state
        private GameObject activeShield = null;
        private bool shieldExpanding = false;
        private bool shieldShrinking = false;
        private float shieldScale = 0f;

        public enum PlayerAction
        {
            Neutral,
            Shoot,
            Shield
        }

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.isKinematic = true;

            if (shipModel != null)
            {
                shipOriginalRotation = shipModel.localRotation;
            }
        }

        void Start()
        {
            if (laneSystem == null)
                laneSystem = FindObjectOfType<LaneSystem>();

            currentLane = 2;
            targetLane = 2;
            SnapToLane(currentLane);

            // Create shield object (hidden initially)
            CreateShield();
        }

        void Update()
        {
            HandleKeyboardInput();

            if (isMoving)
                UpdateMovement();

            UpdateShootVFX();
            UpdateShieldVFX();
            ProcessQueuedInput();
        }

        void HandleKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Movement inputs (only if not shield locked)
            if (!movementLocked)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    ProcessDirectionalInput("left");
                }

                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    ProcessDirectionalInput("right");
                }
            }

            // Action inputs
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                ActivateShoot();
            }

            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                ActivateShield();
            }

            // Reset to neutral when released
            if (keyboard.wKey.wasReleasedThisFrame || keyboard.upArrowKey.wasReleasedThisFrame)
            {
                if (currentAction == PlayerAction.Shoot)
                    currentAction = PlayerAction.Neutral;
            }

            if (keyboard.sKey.wasReleasedThisFrame || keyboard.downArrowKey.wasReleasedThisFrame)
            {
                if (currentAction == PlayerAction.Shield)
                    DeactivateShield();
            }
        }

        void ProcessDirectionalInput(string direction)
        {
            float currentTime = Time.time;

            // Check if mashing same direction
            bool isMashing = false;
            if (direction == lastDirection && (currentTime - lastInputTime) < sameDirectionWindow)
            {
                consecutiveSameDir++;
                isMashing = consecutiveSameDir >= 1; // Second tap = mashing
            }
            else
            {
                consecutiveSameDir = 0;
            }

            lastDirection = direction;
            lastInputTime = currentTime;

            // If currently moving, queue the input
            if (isMoving)
            {
                queuedDirection = direction;
                hasQueuedInput = true;
                Debug.Log($"[Player] Queued: {direction.ToUpper()} (mashing: {isMashing})");
                return;
            }

            // Execute movement immediately
            int laneChange = direction == "left" ? -1 : 1;
            MoveLane(laneChange, isMashing);
        }

        void ProcessQueuedInput()
        {
            // When movement finishes, immediately process queued input
            if (!isMoving && hasQueuedInput && !string.IsNullOrEmpty(queuedDirection))
            {
                string direction = queuedDirection;
                queuedDirection = "";
                hasQueuedInput = false;

                int laneChange = direction == "left" ? -1 : 1;
                MoveLane(laneChange, true); // Queued inputs are always boosted

                Debug.Log($"[Player] Executed queued: {direction.ToUpper()}");
            }
        }

        void MoveLane(int direction, bool boosted)
        {
            int newLane = Mathf.Clamp(currentLane + direction, 0, laneSystem.LaneCount - 1);

            if (newLane == currentLane)
            {
                Debug.Log("[Player] Already at edge lane");
                return;
            }

            targetLane = newLane;
            StartMovement(boosted);
        }

        void StartMovement(bool boosted)
        {
            startPosition = transform.position;
            targetPosition = GetLanePosition(targetLane);

            moveProgress = 0f;
            isMoving = true;

            // Boost speed when mashing
            currentSpeed = boosted ? baseMoveSpeed * mashBoostMultiplier : baseMoveSpeed;

            Debug.Log($"[Player] Moving {currentLane} → {targetLane} (speed: {currentSpeed:F1})");
        }

        void UpdateMovement()
        {
            float distance = Vector3.Distance(startPosition, targetPosition);
            moveProgress += (currentSpeed / distance) * Time.deltaTime;
            moveProgress = Mathf.Clamp01(moveProgress);

            float curvedProgress = movementCurve.Evaluate(moveProgress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, curvedProgress);

            if (moveProgress >= 1f)
            {
                transform.position = targetPosition;
                currentLane = targetLane;
                isMoving = false;
            }
        }

        void SnapToLane(int lane)
        {
            transform.position = GetLanePosition(lane);
            isMoving = false;
            moveProgress = 0f;
        }

        Vector3 GetLanePosition(int lane)
        {
            float laneX = laneSystem.GetLaneXPosition(lane);
            return new Vector3(laneX, transform.position.y, transform.position.z);
        }

        // ========== SHOOT ACTION ==========
        void ActivateShoot()
        {
            currentAction = PlayerAction.Shoot;
            isShooting = true;
            shootTimer = shootSpinDuration;

            Debug.Log("[Player] 🔥 SHOOT!");
        }

        void UpdateShootVFX()
        {
            if (!isShooting) return;

            shootTimer -= Time.deltaTime;

            if (shipModel != null)
            {
                // Rapid spin
                float spinAmount = shootSpinSpeed * Time.deltaTime;
                shipModel.Rotate(Vector3.up, spinAmount, Space.Self);
            }

            if (shootTimer <= 0f)
            {
                isShooting = false;

                // Reset rotation
                if (shipModel != null)
                {
                    shipModel.localRotation = shipOriginalRotation;
                }
            }
        }

        // ========== SHIELD ACTION ==========
        void ActivateShield()
        {
            currentAction = PlayerAction.Shield;
            movementLocked = true; // Can't move while shielding

            shieldExpanding = true;
            shieldShrinking = false;

            if (activeShield != null)
            {
                activeShield.SetActive(true);
            }

            Debug.Log("[Player] 🛡️ SHIELD!");
        }

        void DeactivateShield()
        {
            currentAction = PlayerAction.Neutral;
            movementLocked = false;

            shieldExpanding = false;
            shieldShrinking = true;

            Debug.Log("[Player] Shield deactivated");
        }

        void UpdateShieldVFX()
        {
            if (activeShield == null) return;

            // Expand
            if (shieldExpanding)
            {
                shieldScale += shieldExpandSpeed * Time.deltaTime;
                if (shieldScale >= shieldMaxScale)
                {
                    shieldScale = shieldMaxScale;
                    shieldExpanding = false;
                }
            }

            // Shrink
            if (shieldShrinking)
            {
                shieldScale -= shieldShrinkSpeed * Time.deltaTime;
                if (shieldScale <= 0f)
                {
                    shieldScale = 0f;
                    shieldShrinking = false;
                    activeShield.SetActive(false);
                }
            }

            // Apply scale
            activeShield.transform.localScale = Vector3.one * shieldScale;

            // Fade alpha based on scale
            Renderer renderer = activeShield.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = renderer.material;
                Color color = shieldColor;
                color.a = Mathf.Clamp01(shieldScale / shieldMaxScale) * shieldColor.a;
                mat.color = color;
            }
        }

        void CreateShield()
        {
            if (shieldPrefab != null)
            {
                activeShield = Instantiate(shieldPrefab, transform);
                activeShield.transform.localPosition = Vector3.zero;
                activeShield.SetActive(false);
            }
            else
            {
                // Create default sphere
                activeShield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                activeShield.transform.SetParent(transform);
                activeShield.transform.localPosition = Vector3.zero;
                activeShield.transform.localScale = Vector3.zero;

                // Remove collider
                Collider col = activeShield.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Setup material
                Renderer renderer = activeShield.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.SetFloat("_Mode", 3); // Transparent mode
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                    mat.color = shieldColor;
                    renderer.material = mat;
                }

                activeShield.SetActive(false);
            }
        }

        // Public getters
        public int GetCurrentLane() => currentLane;
        public int GetTargetLane() => targetLane;
        public PlayerAction GetCurrentAction() => currentAction;
        public bool IsMoving() => isMoving;
        public bool IsShieldActive() => currentAction == PlayerAction.Shield;
        public bool IsMovementLocked() => movementLocked;
    }
}