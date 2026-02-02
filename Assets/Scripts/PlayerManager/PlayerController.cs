using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LaneSystem laneSystem;
    [SerializeField] private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] private float normalMoveSpeed = 8f;
    [SerializeField] private float dashMoveSpeed = 20f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Input Buffer Settings")]
    [SerializeField] private float bufferWindow = 0.3f;
    [SerializeField] private int maxBufferSize = 2;

    [Header("Action States")]
    [SerializeField] private PlayerAction currentAction = PlayerAction.Neutral;

    [Header("Debug Info")]
    [SerializeField] private int currentLane = 2;
    [SerializeField] private bool isMoving = false;
    [SerializeField] private List<string> inputBuffer = new List<string>();

    private int targetLane = 2;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float moveProgress = 0f;
    private float currentMoveSpeed = 8f;
    private bool isDashing = false;

    private List<BufferedInput> buffer = new List<BufferedInput>();

    public enum PlayerAction
    {
        Neutral,
        Shoot,
        Shield
    }

    private struct BufferedInput
    {
        public string direction;
        public float timestamp;
    }

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.freezeRotation = true;
        rb.isKinematic = true;
    }

    void Start()
    {
        if (laneSystem == null)
            laneSystem = FindObjectOfType<LaneSystem>();

        currentLane = 2;
        targetLane = 2;
        SnapToLane(currentLane);
    }

    void Update()
    {
        HandleKeyboardInput();
        CleanupBuffer();

        if (isMoving)
            UpdateMovement();

        UpdateDebugInfo();
    }

    void HandleKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // LEFT
        if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
        {
            ProcessDirectionalInput("left");
        }

        // RIGHT
        if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
        {
            ProcessDirectionalInput("right");
        }

        // ACTION UP
        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
        {
            currentAction = PlayerAction.Shoot;
            Debug.Log("[Player] Action: SHOOT");
        }
        if (keyboard.wKey.wasReleasedThisFrame || keyboard.upArrowKey.wasReleasedThisFrame)
        {
            currentAction = PlayerAction.Neutral;
        }

        // ACTION DOWN
        if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
        {
            currentAction = PlayerAction.Shield;
            Debug.Log("[Player] Action: SHIELD");
        }
        if (keyboard.sKey.wasReleasedThisFrame || keyboard.downArrowKey.wasReleasedThisFrame)
        {
            currentAction = PlayerAction.Neutral;
        }
    }

    void ProcessDirectionalInput(string direction)
    {
        BufferedInput input = new BufferedInput
        {
            direction = direction,
            timestamp = Time.time
        };

        buffer.Add(input);
        if (buffer.Count > maxBufferSize)
            buffer.RemoveAt(0);

        bool shouldDash = CheckForDoubleTap(direction);

        int laneChange = direction == "left" ? -1 : 1;
        MoveLane(laneChange, shouldDash);

        Debug.Log($"[Player] Input: {direction.ToUpper()} | Dash: {shouldDash}");
    }

    bool CheckForDoubleTap(string direction)
    {
        if (buffer.Count < 2) return false;

        BufferedInput current = buffer[^1];
        BufferedInput previous = buffer[^2];

        if (current.direction != previous.direction) return false;
        if (current.direction != direction) return false;

        float timeDelta = current.timestamp - previous.timestamp;
        return timeDelta <= bufferWindow;
    }

    void CleanupBuffer()
    {
        float currentTime = Time.time;
        buffer.RemoveAll(input => currentTime - input.timestamp > bufferWindow);
    }

    void MoveLane(int direction, bool dash)
    {
        int newLane = Mathf.Clamp(currentLane + direction, 0, laneSystem.LaneCount - 1);

        if (newLane == currentLane && !isMoving)
            return;

        if (isMoving && newLane != targetLane)
        {
            currentLane = GetClosestLane();
            SnapToLane(currentLane);
        }

        targetLane = newLane;
        StartMovement(dash);
    }

    void StartMovement(bool dash)
    {
        startPosition = transform.position;
        targetPosition = GetLanePosition(targetLane);

        moveProgress = 0f;
        isMoving = true;
        isDashing = dash;
        currentMoveSpeed = dash ? dashMoveSpeed : normalMoveSpeed;
    }

    void UpdateMovement()
    {
        float distance = Vector3.Distance(startPosition, targetPosition);
        moveProgress += (currentMoveSpeed / distance) * Time.deltaTime;
        moveProgress = Mathf.Clamp01(moveProgress);

        float curvedProgress = movementCurve.Evaluate(moveProgress);
        transform.position = Vector3.Lerp(startPosition, targetPosition, curvedProgress);

        if (moveProgress >= 1f)
        {
            transform.position = targetPosition;
            currentLane = targetLane;
            isMoving = false;
            isDashing = false;
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

    int GetClosestLane()
    {
        return laneSystem.GetClosestLane(transform.position);
    }

    void UpdateDebugInfo()
    {
        inputBuffer.Clear();
        foreach (var input in buffer)
        {
            float age = Time.time - input.timestamp;
            inputBuffer.Add($"{input.direction} ({age:F2}s ago)");
        }
    }

    public int GetCurrentLane() => currentLane;
    public int GetTargetLane() => targetLane;
    public PlayerAction GetCurrentAction() => currentAction;
    public bool IsMoving() => isMoving;
    public bool IsDashing() => isDashing;
}
