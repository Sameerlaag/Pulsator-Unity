using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SceneHandler;
using UnityEngine;

/// <summary>
/// Parses osu!mania .osu files and converts them to RhythmNote format
/// </summary>
public class OsuManiaParser
{
    // osu!mania file format reference:
    // [HitObjects]
    // x,y,time,type,hitSound,endTime:hitSample
    // x position determines the lane (based on number of keys)
    
    private const int OSU_PLAYFIELD_WIDTH = 512;
    
    public class ParseResult
    {
        public List<RhythmMapData.RhythmNote> notes = new List<RhythmMapData.RhythmNote>();
        public string audioFilename;
        public float bpm;
        public string title;
        public string artist;
        public int keyCount;
        public bool success;
        public string error;
    }
    
    public static ParseResult ParseOsuFile(string filePath, int targetLanes = 3)
    {
        ParseResult result = new ParseResult();
        
        if (!File.Exists(filePath))
        {
            result.error = $"File not found: {filePath}";
            return result;
        }
        
        try
        {
            string[] lines = File.ReadAllLines(filePath);
            
            // Parse different sections
            string currentSection = "";
            List<string> timingPoints = new List<string>();
            List<string> hitObjects = new List<string>();
            int circleSize = 4; // Default to 4k
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                
                // Section headers
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed;
                    continue;
                }
                
                // Parse based on section
                if (currentSection == "[General]")
                {
                    if (trimmed.StartsWith("AudioFilename:"))
                    {
                        result.audioFilename = trimmed.Substring("AudioFilename:".Length).Trim();
                    }
                }
                else if (currentSection == "[Metadata]")
                {
                    if (trimmed.StartsWith("Title:"))
                    {
                        result.title = trimmed.Substring("Title:".Length).Trim();
                    }
                    else if (trimmed.StartsWith("Artist:"))
                    {
                        result.artist = trimmed.Substring("Artist:".Length).Trim();
                    }
                }
                else if (currentSection == "[Difficulty]")
                {
                    if (trimmed.StartsWith("CircleSize:"))
                    {
                        string csValue = trimmed.Substring("CircleSize:".Length).Trim();
                        if (int.TryParse(csValue, out int cs))
                        {
                            circleSize = cs;
                            result.keyCount = cs;
                        }
                    }
                }
                else if (currentSection == "[TimingPoints]")
                {
                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("//"))
                    {
                        timingPoints.Add(trimmed);
                    }
                }
                else if (currentSection == "[HitObjects]")
                {
                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("//"))
                    {
                        hitObjects.Add(trimmed);
                    }
                }
            }
            
            // Get BPM from first timing point
            if (timingPoints.Count > 0)
            {
                result.bpm = GetBPMFromTimingPoint(timingPoints[0]);
            }
            
            // Convert hit objects to RhythmNotes
            result.notes = ConvertHitObjectsToNotes(hitObjects, circleSize, targetLanes);
            
            result.success = true;
            Debug.Log($"[OsuParser] Parsed {result.notes.Count} notes from {result.title} by {result.artist}");
            Debug.Log($"[OsuParser] Original key count: {circleSize}K → Converted to {targetLanes} lanes");
            
        }
        catch (Exception e)
        {
            result.error = $"Parse error: {e.Message}";
            result.success = false;
        }
        
        return result;
    }
    
    private static float GetBPMFromTimingPoint(string timingPoint)
    {
        // Format: time,beatLength,meter,sampleSet,sampleIndex,volume,uninherited,effects
        string[] parts = timingPoint.Split(',');
        
        if (parts.Length >= 2 && float.TryParse(parts[1], out float beatLength))
        {
            // BPM = 60000 / beatLength
            return 60000f / beatLength;
        }
        
        return 120f; // Default
    }
    
    private static List<RhythmMapData.RhythmNote> ConvertHitObjectsToNotes(List<string> hitObjects, int originalKeyCount, int targetLanes)
    {
        List<RhythmMapData.RhythmNote> notes = new List<RhythmMapData.RhythmNote>();
        
        foreach (string hitObject in hitObjects)
        {
            RhythmMapData.RhythmNote note = ParseHitObject(hitObject, originalKeyCount, targetLanes);
            if (note != null)
            {
                notes.Add(note);
            }
        }
        
        return notes;
    }
    
    private static RhythmMapData.RhythmNote ParseHitObject(string hitObject, int originalKeyCount, int targetLanes)
    {
        // Format: x,y,time,type,hitSound,endTime:hitSample
        string[] parts = hitObject.Split(',');
        
        if (parts.Length < 4)
        {
            Debug.LogWarning($"[OsuParser] Invalid hit object: {hitObject}");
            return null;
        }
        
        // Parse values
        if (!int.TryParse(parts[0], out int x)) return null;
        if (!int.TryParse(parts[2], out int timeMs)) return null;
        if (!int.TryParse(parts[3], out int type)) return null;
        
        // Convert time from milliseconds to seconds
        float timestamp = timeMs / 1000f;
        
        // Calculate lane from x position
        // osu!mania divides the playfield (512 width) into equal columns
        int originalLane = CalculateLaneFromX(x, originalKeyCount);
        
        // Map to our target lane count
        int targetLane = MapLaneToTarget(originalLane, originalKeyCount, targetLanes);
        
        // Determine note type
        // For now, we'll distribute note types based on lane and timing
        // You can customize this logic later
        RhythmMapData.RhythmNote.NoteType noteType = DetermineNoteType(targetLane, timestamp);
        
        return new RhythmMapData.RhythmNote
        {
            timestamp = timestamp,
            lane = targetLane,
            type = noteType
        };
    }
    
    private static int CalculateLaneFromX(int x, int keyCount)
    {
        // osu playfield is 512 wide
        // Divide it into keyCount columns
        float columnWidth = OSU_PLAYFIELD_WIDTH / (float)keyCount;
        int lane = Mathf.FloorToInt(x / columnWidth);
        
        // Clamp to valid range
        return Mathf.Clamp(lane, 0, keyCount - 1);
    }
    
    private static int MapLaneToTarget(int originalLane, int originalKeyCount, int targetLanes)
    {
        // Map from original key count to target lanes
        // Example: 4K → 3 lanes, 7K → 3 lanes
        
        if (originalKeyCount == targetLanes)
        {
            return originalLane;
        }
        
        // Simple proportional mapping
        float ratio = (float)originalLane / (originalKeyCount - 1);
        int targetLane = Mathf.RoundToInt(ratio * (targetLanes - 1));
        
        return Mathf.Clamp(targetLane, 0, targetLanes - 1);
    }
    
    private static RhythmMapData.RhythmNote.NoteType DetermineNoteType(int lane, float timestamp)
    {
        // You can customize this logic based on your game design
        // For now, we'll use a simple pattern:
        
        // Option 1: Cycle through types
        int typeIndex = Mathf.FloorToInt(timestamp * 2) % 3;
        
        // Option 2: Lane-based (commented out)
        // int typeIndex = lane % 3;
        
        return (RhythmMapData.RhythmNote.NoteType)typeIndex;
    }
}
