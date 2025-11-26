using System.Collections.Generic;

[System.Serializable]
public class MapData
{
    public string clipName;
    public List<EMapNote> notes;
}

[System.Serializable]
public class EMapNote
{
    public float time;
    public int lane;      // 0 to 4 
    public float power;   // 0.0 to 1.0
    public NoteType type; // New field for gameplay logic
}

public enum NoteType
{
    Standard, // Shoot (Blue)
    Heavy,    // Dodge (Red)
    Special   // Collect (Green/Yellow) - reserved for future
}