using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RhythmGameDirector : MonoBehaviour
{
    [Header("Controllers")]
    // References to the three ship scripts, NOT the Transforms
    public UFOLaneController leftShipController;

    public UFOLaneController centerShipController;
    public UFOLaneController rightShipController;

    [Header("References")] public AudioMapGenerator mapGenerator;
    public AudioSource musicSource;
    public Transform playerShip;

    [Header("Game Settings")] [Tooltip("How many seconds it takes for a cube to fly from Ship to Player")]
    public float noteTravelTime = 2.0f;

    [Tooltip("Distance between lanes")] public float laneWidth = 2.5f;

    [Tooltip("Optional delay before starting music")]
    public float startDelay = 1f;

    private float directorTime = 0f;
    private bool startedMusic = false;

    public PlayerLocomotion playerLocomotion;
    [Header("Audio")] public LaneHitSoundController hitSoundController;
    [Header("Debug")] public bool autoStartOnMapReady = true;

    private int score = 0;

    private List<EMapNote> notes;
    private int nextNoteIndex = 0;
    private bool isPlaying = false;

    // Lane positions 
    private float[] laneXPositions;

    // ... (UI and Leaderboard setup remains the same) ...
    [Header("Ranking System")] public ScoreManager scoreManager;
    public TMP_Text leaderboardText;

    [Header("UI")] public GameObject scorePanel;
    public GameObject gameOverPanel;
    public TMP_Text sideScoreText;
    public TMP_Text finalScoreText;
    public Button playAgainButton;
    [Header("Main Menu")] public GameObject mainMenuPanel;
    public bool requireStartClick = true;
    private bool hasStarted = false;
    private bool hasEnded = false;


    private void Start()
    {
        // ... (UI setup) ...
        finalScoreText.gameObject.SetActive(false);
        gameOverPanel.SetActive(false);
        musicSource.Stop();

        // Calculate lane X positions
        laneXPositions = new float[mapGenerator.lanes];
        float playerX = playerShip.position.x;

        for (int i = 0; i < laneXPositions.Length; i++)
        {
            laneXPositions[i] = playerX + ((i - 2) * laneWidth);
        }

        // Initialize all Ship Controllers
        InitializeShipController(leftShipController);
        InitializeShipController(centerShipController);
        InitializeShipController(rightShipController);

        // Wait for map generation
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerationComplete += OnMapReady;
        }
        else
        {
            Debug.LogError("[Director] No BeatSyncedMapGenerator assigned!");
        }
    }

    private void InitializeShipController(UFOLaneController controller)
    {
        if (controller != null)
        {
            controller.gameDirector = this;
            controller.allLaneXPositions = laneXPositions;
            controller.hitSoundController = hitSoundController;
            controller.noteTravelTime = noteTravelTime;
            controller.playerShip = playerShip;
            // Awake handles default position, no need for an explicit Initialize here.
        }
        else
        {
            Debug.LogError("[Director] Missing a UFOLaneController reference!");
        }
    }

    private void OnMapReady()
    {
        notes = mapGenerator.mapNotes;
        notes.Sort((a, b) => a.time.CompareTo(b.time));

        if (playerLocomotion != null)
        {
            playerLocomotion.allLaneXPositions = laneXPositions;
            Vector3 pos = playerLocomotion.transform.position;
            pos.x = laneXPositions[playerLocomotion.currentLane];
            playerLocomotion.transform.position = pos;
        }

        Debug.Log($"[Director] Map ready with {notes.Count} notes.");

        // If main menu required, do NOT auto start
        if (requireStartClick)
        {
            isPlaying = false;
            return;
        }

        // Otherwise run normally
        if (autoStartOnMapReady)
            StartCoroutine(StartGameRoutine());
    }

    public void StartGameFromMenu()
    {
        if (hasStarted) return;
        isPlaying = true;
        hasStarted = true;
        mainMenuPanel.SetActive(false);

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        ResetShips(); // reset positions to overlap center
        score = 0;

        // Show UI
        sideScoreText.gameObject.SetActive(true);
        finalScoreText.gameObject.SetActive(false);
        gameOverPanel.SetActive(false);
        playAgainButton.gameObject.SetActive(false);
        scorePanel.SetActive(true);
        sideScoreText.text = "0";

        // === INITIAL SHIP ANIMATION ===
        float moveDuration = 0.3f; // 0.2–0.3 sec is fast enough
        leftShipController.MoveToInitialLane(laneXPositions[0], moveDuration);
        centerShipController.MoveToInitialLane(laneXPositions[2], moveDuration);
        rightShipController.MoveToInitialLane(laneXPositions[4], moveDuration);

        // Wait until they reach lanes
        yield return new WaitForSeconds(moveDuration);

        directorTime = 0f;
        startedMusic = false;
        isPlaying = true;
    }


    private void Update()
    {
        if (!isPlaying)
            return;

        // ... (Time tracking remains the same) ...
        directorTime += Time.unscaledDeltaTime;

        if (!startedMusic && directorTime >= startDelay)
        {
            startedMusic = true;
            musicSource.Play();
        }

        if (!startedMusic)
            return;

        float musicTime = musicSource.timeSamples / (float)musicSource.clip.frequency;
        float spawnTime = musicTime + noteTravelTime;

        while (nextNoteIndex < notes.Count && notes[nextNoteIndex].time <= spawnTime)
        {
            // NEW: Delegate spawning to the correct ship controller
            SpawnNote(notes[nextNoteIndex]);
            nextNoteIndex++;
        }

        // Ship position updating is now handled independently in each UFOLaneController's Update

        // Stop when finished
        if (nextNoteIndex >= notes.Count && !musicSource.isPlaying)
        {
            isPlaying = false;
            Debug.Log("[Director] Song finished!");
            OnGameOver();
        }
    }


    [ContextMenu("Manual Start")]
    public void ManualStart()
    {
        if (notes == null || notes.Count == 0)
        {
            Debug.LogError("[Director] No map loaded!");
            return;
        }

        StartCoroutine(StartGameRoutine());
    }


    private void OnGameOver()
    {
        Debug.Log("[Director] Game Over!");

        // Hide gameplay UI
        sideScoreText.gameObject.SetActive(false);

        // Add score to leaderboard
        scoreManager.AddScore(score);

        // Show game over UI
        scorePanel.SetActive(true);
        gameOverPanel.SetActive(true);
        finalScoreText.gameObject.SetActive(true);
        playAgainButton.gameObject.SetActive(true);

        finalScoreText.text = "Final Score: " + score;

        UpdateLeaderboardDisplay();
        hasEnded =  true;
    }

    private void UpdateLeaderboardDisplay()
    {
        List<ScoreEntry> list = scoreManager.topScores;
        leaderboardText.text = "";

        foreach (var entry in list)
        {
            bool isPlayerScore = entry.score == score;

            if (isPlayerScore)
            {
                leaderboardText.text += $"<size=150%><b>{entry.score}</b></size>\n"
                                        + $"<size=70%>{entry.date}</size>\n\n";
            }
            else
            {
                leaderboardText.text += $"{entry.score}\n"
                                        + $"<size=70%>{entry.date}</size>\n\n";
            }
        }
    }


    private void SpawnNote(EMapNote note)
    {
        int targetLane = note.lane;

        UFOLaneController shipToUse;

        // Decide which ship fires
        if (targetLane <= 1)
            shipToUse = leftShipController;
        else if (targetLane == 2)
            shipToUse = centerShipController;
        else
            shipToUse = rightShipController;

        // Reset the other two ships
        if (shipToUse != leftShipController) leftShipController.ResetShipPosition();
        if (shipToUse != centerShipController) centerShipController.ResetShipPosition();
        if (shipToUse != rightShipController) rightShipController.ResetShipPosition();

        // Fire the note
        shipToUse.SpawnNote(note);
    }


    [ContextMenu("Reset Ships")]
    public void ResetShips()
    {
        // Tell each ship controller to reset itself
        leftShipController.ResetShipPosition();
        centerShipController.ResetShipPosition();
        rightShipController.ResetShipPosition();
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
            mapGenerator.OnMapGenerationComplete -= OnMapReady;
    }

    // === DEBUG HELPERS ===
    private void OnDrawGizmosSelected()
    {
        if (playerShip == null || laneXPositions == null) return;

        // Draw lane positions
        Gizmos.color = Color.cyan;
        for (int i = 0; i < laneXPositions.Length; i++)
        {
            Vector3 start = new Vector3(laneXPositions[i], playerShip.position.y - 1f, playerShip.position.z);
            Vector3 end = new Vector3(laneXPositions[i], playerShip.position.y + 1f, playerShip.position.z);
            Gizmos.DrawLine(start, end);
        }
    }

// The rest of the methods are unchanged
    public void updateScore(int i)
    {
        score += i;
        sideScoreText.text = score.ToString();
    }

    public void RestartScene()
    {
        if (hasEnded)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}