using System;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerLocomotion : MonoBehaviour
{
    private Rigidbody rb;
    private PlayerInputManager inputs;

    [Header("Lane Settings")] 
    public int currentLane = 0;
    private int targetLane = 0;
    public float laneOffset = 2.5f;
    
    // Updated to use the definitive external lane positions
    [HideInInspector] public float[] allLaneXPositions;
    private float referenceZ;

    [Header("Movement Settings")] 
    public float moveSpeed = 10f;
    public float acceleration = 40f; // Note: Acceleration is unused with Lerp movement
    public float stopThreshold = 0.05f;

    [Header("Spin Settings")] 
    public float spinSpeed = 800f;
    private bool doSpin = false;

    private Queue<int> moveQueue = new Queue<int>();
    private bool isMoving = false;
    private Vector3 moveTarget;

    private float queueTimer = 0f;
    private float queueTimeout = 0.3f;
    
    // Added for clear lane boundary check
    private int minLaneIndex = 0;
    private int maxLaneIndex = 4;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        
        // Removed referenceX as we now use allLaneXPositions
        referenceZ = transform.position.z; 
        
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
        // Must ensure lane positions are initialized before attempting to move
        if (allLaneXPositions == null || allLaneXPositions.Length == 0) return;
        
        // Countdown queue timer and clear if expired
        if (queueTimer > 0f)
        {
            queueTimer -= Time.fixedDeltaTime;
            if (queueTimer <= 0f)
            {
                // Optionally clear the entire queue if the player hesitates
                moveQueue.Clear(); 
            }
        }

        if (!isMoving && moveQueue.Count > 0)
            StartNextMove();

        if (isMoving)
            MoveTowardTarget();
    }

    private void StartNextMove()
    {
        int direction = moveQueue.Dequeue();
        
        // Calculate potential target based on current lane
        int tempTargetLane = currentLane + direction; 
        
        // Use the defined lane boundaries
        targetLane = Mathf.Clamp(tempTargetLane, minLaneIndex, maxLaneIndex);
        
        // Only start moving if the move is actually possible
        if (targetLane != currentLane)
        {
             // Use the officially assigned X position for the move target
            moveTarget = new Vector3(allLaneXPositions[targetLane], transform.position.y, referenceZ);
            
            // Spin only if jumping two lanes or more, although StartNextMove should only handle single steps
            doSpin = Mathf.Abs(targetLane - currentLane) >= 2; 
            isMoving = true;
            Debug.Log($"Starting move from {currentLane} to {targetLane}, Spin: {doSpin}");
        }
        else
        {
            // If the move was invalid (e.g., trying to move left from Lane 0), clear spin and mark as not moving
            doSpin = false;
            isMoving = false; 
        }
    }


    private void MoveTowardTarget()
    {
        Vector3 currentPos = transform.position;
        Vector3 targetPos = moveTarget;

        // Smoothly interpolate toward the target
        // NOTE: Using Lerp makes 'acceleration' header field irrelevant.
        transform.position = Vector3.Lerp(currentPos, targetPos, moveSpeed * Time.fixedDeltaTime);

        // Spin while moving
        if (doSpin)
            transform.Rotate(0, 0, spinSpeed * Time.fixedDeltaTime, Space.Self);

        // Check if arrived (using stopThreshold for smooth interpolation exit)
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
        // 1. Cap the queue size
        if (moveQueue.Count < 3)
        {
            moveQueue.Enqueue(direction);
        }

        // Start/reset queue timeout
        if (queueTimer <= 0f)
            queueTimer = queueTimeout;

        // 2. Dynamic target update logic (only relevant if currently moving)
        if (isMoving && moveQueue.Count > 0)
        {
            // Check if all queued inputs (including the new one) are the same direction
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
                // Calculate how many total steps forward we want to make
                int stepsToTarget = moveQueue.Count;
                int newTargetIndex = Mathf.Clamp(currentLane + (first * stepsToTarget), minLaneIndex, maxLaneIndex);
                
                // --- THE CRITICAL FIX ---
                // We only perform the update if the new target is different from the OLD target.
                // We must also consume the inputs that are now covered by the new target.
                if (newTargetIndex != targetLane)
                {
                    // Calculate how many inputs we need to consume from the queue
                    int distanceCovered = Mathf.Abs(newTargetIndex - currentLane);
                    
                    // The queue length might be longer than the distance we can actually move
                    int inputsToConsume = Mathf.Min(moveQueue.Count, distanceCovered); 
                    
                    // Consume the inputs that are now "part" of the single extended move
                    for (int i = 0; i < inputsToConsume; i++)
                    {
                        moveQueue.Dequeue();
                    }

                    // Reset the target to the new, extended lane
                    targetLane = newTargetIndex;
                    moveTarget = new Vector3(allLaneXPositions[targetLane], transform.position.y, referenceZ);
                    
                    // Check if the jump is 2 lanes or more from the STARTING position of the move
                    doSpin = distanceCovered >= 2;
                    
                    Debug.Log($"Dynamic target update: {currentLane} -> {targetLane}, Spin: {doSpin}");
                }
            }
        }
    }
}