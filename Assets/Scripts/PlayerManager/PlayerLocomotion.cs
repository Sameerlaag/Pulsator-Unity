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
    
    [HideInInspector] public float[] allLaneXPositions;
    private float referenceZ;

    [Header("Movement Settings")] 
    public float moveSpeed = 15f;
    public float arrivalThreshold = 0.02f;
    
    [Header("Spin Settings")]
    public float redirectionSpinSpeed = 720f; // Spin speed when changing direction mid-flight
    public float normalSpinSpeed = 360f; // Spin speed for regular 2+ lane jumps
    public float spinDecaySpeed = 5f; // How fast spin slows down after reaching target
    
    private Queue<int> moveQueue = new Queue<int>();
    private bool isMoving = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float moveProgress = 0f;
    
    // Input buffering
    private float lastInputTime = 0f;
    private float inputBufferWindow = 0.5f;
    
    private int minLaneIndex = 0;
    private int maxLaneIndex = 4;
    
    private int currentMoveDirection = 0; // -1 left, 1 right, 0 none
    private float currentSpinVelocity = 0f; // Current Z-axis rotation speed
    private bool isRedirecting = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        
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
        if (allLaneXPositions == null || allLaneXPositions.Length == 0) return;
        
        // Clear old inputs outside buffer window
        if (Time.time - lastInputTime > inputBufferWindow && !isMoving)
        {
            moveQueue.Clear();
            currentMoveDirection = 0;
        }

        if (!isMoving && moveQueue.Count > 0)
            StartNextMove();

        if (isMoving)
            UpdateMovement();
        else
            UpdateIdleRotation();
    }

    private void StartNextMove()
    {
        int direction = moveQueue.Dequeue();
        
        // Check if this is a redirection (changing direction mid-flight)
        if (isMoving && currentMoveDirection != 0 && direction != currentMoveDirection)
        {
            isRedirecting = true;
            // Apply spin in the direction of the new movement
            currentSpinVelocity = direction * redirectionSpinSpeed;
            Debug.Log($"Redirecting! Spin velocity: {currentSpinVelocity}");
        }
        
        currentMoveDirection = direction;
        
        int tempTargetLane = currentLane + direction;
        targetLane = Mathf.Clamp(tempTargetLane, minLaneIndex, maxLaneIndex);
        
        if (targetLane != currentLane)
        {
            startPosition = transform.position;
            targetPosition = new Vector3(allLaneXPositions[targetLane], transform.position.y, referenceZ);
            
            int totalDistance = Mathf.Abs(targetLane - currentLane);
            
            // If not redirecting and jumping 2+ lanes, apply normal spin
            if (!isRedirecting && totalDistance >= 2)
            {
                currentSpinVelocity = direction * normalSpinSpeed;
            }
            
            moveProgress = 0f;
            isMoving = true;
        }
    }

    private void UpdateMovement()
    {
        // Smooth acceleration curve
        moveProgress += moveSpeed * Time.fixedDeltaTime;
        float t = Mathf.SmoothStep(0f, 1f, moveProgress);
        
        transform.position = Vector3.Lerp(startPosition, targetPosition, t);

        // Apply Z-axis spin
        if (Mathf.Abs(currentSpinVelocity) > 0.1f)
        {
            transform.Rotate(0, 0, currentSpinVelocity * Time.fixedDeltaTime, Space.Self);
            
            // Gradually slow down spin as we approach target
            if (moveProgress > 0.7f)
            {
                currentSpinVelocity = Mathf.Lerp(currentSpinVelocity, 0f, spinDecaySpeed * Time.fixedDeltaTime);
            }
        }

        // Check arrival
        if (Vector3.Distance(transform.position, targetPosition) < arrivalThreshold || moveProgress >= 1f)
        {
            transform.position = targetPosition;
            currentLane = targetLane;
            
            // Check if there's a queued move
            bool hasChainedMove = false;
            if (moveQueue.Count > 0)
            {
                int nextDirection = moveQueue.Peek();
                
                // Check if it's the same direction (smooth chain) or different (redirection)
                if (nextDirection == currentMoveDirection)
                {
                    // Continue in same direction without stopping
                    hasChainedMove = true;
                    isMoving = false; // Will restart immediately
                    isRedirecting = false;
                }
                else
                {
                    // Different direction = redirection on next move
                    hasChainedMove = true;
                    isMoving = false;
                    // Don't reset isRedirecting yet - will be set in StartNextMove
                }
            }
            
            if (!hasChainedMove)
            {
                isMoving = false;
                currentMoveDirection = 0;
                isRedirecting = false;
                // Spin will decay in UpdateIdleRotation
            }
        }
    }

    private void UpdateIdleRotation()
    {
        // Smoothly stop any remaining spin
        if (Mathf.Abs(currentSpinVelocity) > 0.1f)
        {
            currentSpinVelocity = Mathf.Lerp(currentSpinVelocity, 0f, spinDecaySpeed * Time.fixedDeltaTime);
            transform.Rotate(0, 0, currentSpinVelocity * Time.fixedDeltaTime, Space.Self);
        }
        else
        {
            currentSpinVelocity = 0f;
            // Gradually normalize Z rotation to 0
            Vector3 euler = transform.rotation.eulerAngles;
            float zRot = euler.z;
            if (zRot > 180f) zRot -= 360f;
            
            if (Mathf.Abs(zRot) > 0.1f)
            {
                float newZ = Mathf.Lerp(zRot, 0f, spinDecaySpeed * Time.fixedDeltaTime);
                transform.rotation = Quaternion.Euler(euler.x, euler.y, newZ);
            }
        }
    }
    
    private void HandleMoveInput(int direction)
    {
        lastInputTime = Time.time;
        
        // Cap queue size
        if (moveQueue.Count < 4)
        {
            moveQueue.Enqueue(direction);
        }

        // Dynamic target update if moving in same direction
        if (isMoving && moveQueue.Count > 0)
        {
            bool allSameDirection = true;
            int firstDir = moveQueue.Peek();
            
            foreach (int move in moveQueue)
            {
                if (move != firstDir)
                {
                    allSameDirection = false;
                    break;
                }
            }

            if (allSameDirection && firstDir == currentMoveDirection)
            {
                int stepsToTarget = moveQueue.Count;
                int newTargetIndex = Mathf.Clamp(currentLane + (firstDir * stepsToTarget), minLaneIndex, maxLaneIndex);
                
                if (newTargetIndex != targetLane)
                {
                    int distanceCovered = Mathf.Abs(newTargetIndex - currentLane);
                    int inputsToConsume = Mathf.Min(moveQueue.Count, distanceCovered);
                    
                    for (int i = 0; i < inputsToConsume; i++)
                    {
                        moveQueue.Dequeue();
                    }

                    // Update target without resetting progress
                    targetLane = newTargetIndex;
                    targetPosition = new Vector3(allLaneXPositions[targetLane], transform.position.y, referenceZ);
                    
                    // Don't change spin when extending in same direction
                    Debug.Log($"Extending move: {currentLane} -> {targetLane}");
                }
            }
        }
    }
}