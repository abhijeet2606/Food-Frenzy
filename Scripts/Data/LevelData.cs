using System.Collections.Generic;

[System.Serializable]
public class LevelContainer
{
    public List<LevelData> levels;
}

[System.Serializable]
public class LevelData
{
    public int levelId;
    public int rows;
    public int columns;
    public int moves;
    public string[] foodTypes;
    public LevelGoalData[] goals;
}

[System.Serializable]
public class LevelGoalData
{
    public string type;
    public int count;
}