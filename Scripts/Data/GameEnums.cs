using System;

/// <summary>
/// Bonus types for special food items
/// </summary>
[Flags]
public enum BonusType
{
    None = 0,
    // CrossKnife = 1 << 0,
    Pan = 1 << 1,
    Oven = 1 << 2,
    HorizontalKnife = 1 << 3,
    VerticalKnife = 1 << 4,
    Flies = 1 << 5
}

public static class BonusTypeUtilities
{
    /// <summary>
    /// Helper method to check for specific bonus type
    /// </summary>
    public static bool ContainsFlies(BonusType bt)
    {
        return (bt & BonusType.Flies) == BonusType.Flies;
    }

    public static bool ContainsLinearKnife(BonusType bt)
    {
        return 
               (bt & BonusType.HorizontalKnife) == BonusType.HorizontalKnife ||
               (bt & BonusType.VerticalKnife) == BonusType.VerticalKnife;
    }

    public static bool ContainsHorizontalKnife(BonusType bt)
    {
        return (bt & BonusType.HorizontalKnife) == BonusType.HorizontalKnife;
    }

    public static bool ContainsVerticalKnife(BonusType bt)
    {
        return (bt & BonusType.VerticalKnife) == BonusType.VerticalKnife;
    }

    public static bool ContainsPan(BonusType bt)
    {
        return (bt & BonusType.Pan) == BonusType.Pan;
    }

    public static bool ContainsOven(BonusType bt)
    {
        return (bt & BonusType.Oven) == BonusType.Oven;
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
