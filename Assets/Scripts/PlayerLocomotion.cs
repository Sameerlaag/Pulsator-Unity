using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerLocomotion : MonoBehaviour
{
    private Rigidbody rb;
    private PlayerInputManager inputs;

    [Header("Lane Settings")] public int currentLane = 0;
    private int targetLane = 0;
    public float laneOffset = 2.5f;
    private float referenceX;

    [Header("Movement Settings")] public float moveSpeed = 10f;
    public float acceleration = 40f;
    public float stopThreshold = 0.05f;

    [Header("Spin Settings")] public float spinSpeed = 800f;
    private bool doSpin = false;

    private Queue<int> moveQueue = new Queue<int>();
    private bool isMoving = false;
    private Vector3 moveTarget;

    private float queueTimer = 0f;
    private float queueTimeout = 0.3f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        referenceX = transform.position.x;

        inputs = GetComponent<PlayerInputManager>();
        if (inputs != null)
            inputs.OnMoveInput += HandleMoveInput;
    }

    void OnDestroy()
    {
        if (inputs != null)
            inputs.OnMoveInput -= HandleMoveInput;
    }

    void FixedUpdate()
    {
        // Countdown queue timer and clear if expired
        if (queueTimer > 0f)
        {
            queueTimer -= Time.fixedDeltaTime;
            if (queueTimer <= 0f)
                moveQueue.Clear();
        }

        if (!isMoving && moveQueue.Count > 0)
            StartNextMove();

        if (isMoving)
            MoveTowardTarget();
    }

    private void StartNextMove()
    {
        int direction = moveQueue.Dequeue();
        targetLane = Mathf.Clamp(currentLane + direction, -2, 2);
        moveTarget = new Vector3(referenceX + targetLane * laneOffset, transform.position.y, transform.position.z);
        doSpin = Mathf.Abs(targetLane - currentLane) >= 2 || targetLane == currentLane;
        isMoving = true;

        Debug.Log($"Starting move from {currentLane} to {targetLane}, Spin: {doSpin}");
    }


    private void MoveTowardTarget()
    {
        Vector3 currentPos = transform.position;
        Vector3 targetPos = moveTarget;

        // Smoothly interpolate toward the target
        transform.position = Vector3.Lerp(currentPos, targetPos, moveSpeed * Time.fixedDeltaTime);

        // Spin while moving
        if (doSpin)
            transform.Rotate(0, 0, spinSpeed * Time.fixedDeltaTime, Space.Self);

        // Check if arrived (use stopThreshold for smooth interpolation)
        if (Vector3.Distance(transform.position, targetPos) < stopThreshold)
        {
            // Snap exactly to target position
            transform.position = targetPos;
            currentLane = targetLane;
            isMoving = false;

            // Reset rotation so z always ends at 0
            Vector3 euler = transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(euler.x, euler.y, 0f);

            // Reset spin flag
            doSpin = false;
        }
    }
    
    private void HandleMoveInput(int direction)
    {
        if (moveQueue.Count < 3)
        {
            moveQueue.Enqueue(direction);
        }

        // Start/reset queue timeout
        if (queueTimer <= 0f)
            queueTimer = queueTimeout;

        // If moving, check if all queued inputs are the same direction
        if (isMoving && moveQueue.Count > 0)
        {
            bool allSame = true;
            int first = moveQueue.Peek();
            foreach (int move in moveQueue)
            {
                if (move != first)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                // Update targetLane dynamically
                int newTarget = Mathf.Clamp(targetLane + first, -2, 2);
                if (newTarget != targetLane)
                {
                    targetLane = newTarget;
                    moveTarget = new Vector3(referenceX + targetLane * laneOffset, transform.position.y,
                        transform.position.z);
                    doSpin = Mathf.Abs(targetLane - currentLane) >= 2;
                    Debug.Log($"Dynamic target update: {currentLane} -> {targetLane}, Spin: {doSpin}");
                }
            }
        }
    }
}