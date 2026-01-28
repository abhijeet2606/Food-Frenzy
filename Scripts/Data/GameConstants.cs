using UnityEngine;

public static class GameConstants
{
    public static int Rows = 6;
    public static int Columns = 6;

    public static readonly float AnimationDuration = 0.2f;

    public static readonly float MoveAnimationMinDuration = 0.05f;

    public static readonly float ExplosionDuration = 0.3f;

    public static readonly float WaitBeforePotentialMatchesCheck = 2f;
    public static readonly float OpacityAnimationFrameDelay = 0.05f;

    public static readonly int MinimumMatches = 3;
    public static readonly int MinimumMatchesForBonus = 4;

    public static readonly int Match3Score = 60;
    public static readonly int SubsequentMatchScore = 1000;
}
