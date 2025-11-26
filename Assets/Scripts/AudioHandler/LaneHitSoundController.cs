using UnityEngine;

/// <summary>
/// Handles playing unique audio feedback sounds based on the lane index
/// when the player successfully hits a note.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class LaneHitSoundController : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The AudioSource used to play the hit sounds.")]
    private AudioSource audioSource;

    [Tooltip("An array of audio clips, where index 0 is for Lane 0, index 1 for Lane 1, and so on. Must contain 5 clips.")]
    public AudioClip[] laneHitSounds = new AudioClip[5];

    private void Awake()
    {
        // Get or add the AudioSource component
        audioSource = GetComponent<AudioSource>();
        
        // Optional: Configure the AudioSource for sound effects
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    /// <summary>
    /// Plays the corresponding audio clip for the given lane index.
    /// </summary>
    /// <param name="laneIndex">The index of the lane (0 to 4).</param>
    public void PlayHitSound(int laneIndex)
    {
        // Safety check 1: Ensure the array is configured
        if (laneHitSounds == null || laneHitSounds.Length != 5)
        {
            Debug.LogError("[LaneSound] laneHitSounds array is not correctly set up (needs 5 elements)!");
            return;
        }

        // Safety check 2: Ensure the lane index is valid
        if (laneIndex < 0 || laneIndex >= laneHitSounds.Length)
        {
            Debug.LogWarning($"[LaneSound] Invalid lane index ({laneIndex}). Cannot play sound.");
            return;
        }

        AudioClip clipToPlay = laneHitSounds[laneIndex];

        // Safety check 3: Ensure the specific clip exists
        if (clipToPlay != null)
        {
            // Play the clip immediately on the assigned AudioSource
            audioSource.PlayOneShot(clipToPlay);
        }
        else
        {
            Debug.LogWarning($"[LaneSound] No audio clip assigned for Lane {laneIndex}.");
        }
    }
}