using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneHandler
{
    [CreateAssetMenu(fileName = "NewRhythmMap", menuName = "Rhythm Game/Map")]
    public class RhythmMapData : ScriptableObject
    {
        [Header("Map Info")]
        public string mapName;
        public AudioClip musicTrack;
        public float bpm = 120f;
    
        [Header("Timing")]
        public float noteSpeed = 10f;           // Units per second the notes travel
        public float noteSpawnDistance = 50f;   // How far away notes spawn
    
        [Header("Notes")]
        public List<RhythmNote> notes = new List<RhythmNote>();
    
        // Calculated property: time it takes for a note to reach player
        public float NoteTimeToTravel => noteSpawnDistance / noteSpeed;
    
        // Helper to get spawn time (when to instantiate the note)
        public float GetSpawnTime(float hitTime)
        {
            return hitTime - NoteTimeToTravel;
        }
    
    
    
        [Serializable]
        public class RhythmNote
        {
            public float timestamp;      // When this note should reach the player (in seconds)
            public int lane;            // 0, 1, 2, or 3 (left to right)
            public NoteType type;       // What kind of note this is
    
            public enum NoteType
            {
                Absorb,    // Neutral - just catch it
                Shoot,     // Up - needs laser
                Shield     // Down - needs barrier
            }
        }
    }
}
