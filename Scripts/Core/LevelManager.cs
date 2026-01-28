using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;
    private Dictionary<int, LevelData> levels;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
            LoadAllLevels();
        }
        else Destroy(gameObject);
    }

    private void LoadAllLevels()
    {
        levels = new Dictionary<int, LevelData>();
        TextAsset file = Resources.Load<TextAsset>("Levels/levels");
        if (file != null)
        {
            LevelContainer container = JsonUtility.FromJson<LevelContainer>(file.text);
            if (container != null && container.levels != null)
            {
                foreach (var level in container.levels)
                {
                    if (!levels.ContainsKey(level.levelId))
                    {
                        levels.Add(level.levelId, level);
                    }
                }
                Debug.Log($"Loaded {levels.Count} levels from JSON.");
            }
        }
        else
        {
            Debug.LogError("Could not find Levels/levels.json file!");
        }
    }

    public LevelData LoadLevel(int levelId)
    {
        if (levels != null && levels.ContainsKey(levelId))
        {
            return levels[levelId];
        }

        // Fallback: If level > max levels, maybe loop or return last level? 
        // For now, let's just return Level 1 if not found, or maybe modulo.
        if (levels != null && levels.Count > 0)
        {
            // Simple loop logic: (levelId - 1) % count + 1
            int loopedId = ((levelId - 1) % levels.Count) + 1;
            Debug.Log($"Level {levelId} not found, looping to {loopedId}");
            if (levels.ContainsKey(loopedId))
                return levels[loopedId];
        }

        Debug.LogWarning($"Level {levelId} not found and no fallback available!");
        return null;
    }

    public string[,] LoadLevelData(string levelName = "level")
    {
        string[,] shapes = new string[GameConstants.Rows, GameConstants.Columns];

        TextAsset txt = Resources.Load(levelName) as TextAsset;
        if (txt == null) return null;
        
        string level = txt.text;

        string[] lines = level.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        for (int row = GameConstants.Rows - 1; row >= 0; row--)
        {
            string[] items = lines[row].Split('|');
            for (int column = 0; column < GameConstants.Columns; column++)
            {
                shapes[row, column] = items[column];
            }
        }
        return shapes;
    }
}
