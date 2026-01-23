using System;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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
