using System;

/// <summary>
/// Bonus types for special food items
/// </summary>
[Flags]
public enum BonusType
{
    None = 0,
    DestroyWholeRowColumn = 1 << 0,
    // Add more types here e.g. Bomb = 1 << 1
}

public static class BonusTypeUtilities
{
    /// <summary>
    /// Helper method to check for specific bonus type
    /// </summary>
    public static bool ContainsDestroyWholeRowColumn(BonusType bt)
    {
        return (bt & BonusType.DestroyWholeRowColumn) == BonusType.DestroyWholeRowColumn;
    }
}

/// <summary>
/// Simple game state
/// </summary>
public enum GameState
{
    None,
    SelectionStarted,
    Animating,
    Win,
    Lose,
    Paused
}
