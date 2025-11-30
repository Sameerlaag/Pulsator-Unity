using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class ScoreEntry
{
    public int score;
    public string date;
}

[Serializable]
public class ScoreData
{
    public List<ScoreEntry> scores = new List<ScoreEntry>();
}

public class ScoreManager : MonoBehaviour
{
    private string filePath;
    public List<ScoreEntry> topScores = new List<ScoreEntry>();

    private void Awake()
    {
        filePath = Path.Combine(Application.persistentDataPath, "scores.json");
        LoadScores();
    }

    public void AddScore(int score)
    {
        // Add new entry
        var entry = new ScoreEntry
        {
            score = score,
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };

        topScores.Add(entry);

        // Sort high → low
        topScores.Sort((a, b) => b.score.CompareTo(a.score));

        // Keep only top 5
        if (topScores.Count > 5)
            topScores = topScores.GetRange(0, 5);

        SaveScores();
    }

    public void SaveScores()
    {
        ScoreData data = new ScoreData();
        data.scores = topScores;

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(filePath, json);
    }

    public void LoadScores()
    {
        if (!File.Exists(filePath))
        {
            topScores = new List<ScoreEntry>();
            return;
        }

        string json = File.ReadAllText(filePath);
        ScoreData data = JsonUtility.FromJson<ScoreData>(json);

        topScores = data?.scores ?? new List<ScoreEntry>();
    }
}