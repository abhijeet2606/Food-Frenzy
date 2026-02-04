using System;

/// <summary>
/// Bonus types for special food items
/// </summary>
[Flags]
public enum BonusType
{
    None = 0,
    DestroyWholeRowColumn = 1 << 0,
    Explosion = 1 << 1,
    ColorBomb = 1 << 2,
    DestroyWholeRow = 1 << 3,
    DestroyWholeColumn = 1 << 4
}

public static class BonusTypeUtilities
{
    /// <summary>
    /// Helper method to check for specific bonus type
    /// </summary>
    public static bool ContainsDestroyWholeRowColumn(BonusType bt)
    {
        return (bt & BonusType.DestroyWholeRowColumn) == BonusType.DestroyWholeRowColumn ||
               (bt & BonusType.DestroyWholeRow) == BonusType.DestroyWholeRow ||
               (bt & BonusType.DestroyWholeColumn) == BonusType.DestroyWholeColumn;
    }

    public static bool ContainsDestroyWholeRow(BonusType bt)
    {
        return (bt & BonusType.DestroyWholeRow) == BonusType.DestroyWholeRow;
    }

    public static bool ContainsDestroyWholeColumn(BonusType bt)
    {
        return (bt & BonusType.DestroyWholeColumn) == BonusType.DestroyWholeColumn;
    }

    public static bool ContainsExplosion(BonusType bt)
    {
        return (bt & BonusType.Explosion) == BonusType.Explosion;
    }

    public static bool ContainsColorBomb(BonusType bt)
    {
        return (bt & BonusType.ColorBomb) == BonusType.ColorBomb;
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
