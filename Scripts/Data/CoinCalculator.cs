using UnityEngine;

public static class CoinCalculator
{
    // 1. Base Coins per Level Range
    public static int GetBaseCoins(int level)
    {
        if (level <= 20) return 20;
        if (level <= 50) return 25;
        if (level <= 100) return 30;
        if (level <= 200) return 35;
        if (level <= 400) return 40;
        return 45;
    }

    // 2. Coins per Remaining Move
    public static int GetCoinPerMove(int level)
    {
        if (level <= 50) return 4;
        if (level <= 150) return 5;
        if (level <= 300) return 6;
        return 7;
    }

    // 3. Difficulty Multiplier
    public static float GetDifficultyMultiplier(string difficulty)
    {
        if (string.IsNullOrEmpty(difficulty)) return 1.0f;
        
        switch (difficulty.ToLower())
        {
            case "hard": return 1.4f;
            case "superhard": 
            case "super hard": return 1.8f;
            default: return 1.0f;
        }
    }

    // 4. Bonus Coins
    public static int GetBonusCoins(bool boostersUsed, bool firstTry, int winStreak)
    {
        int bonus = 0;
        
        // No booster used
        if (!boostersUsed) bonus += 10;
        
        // First try win
        if (firstTry) bonus += 15;
        
        // Win streak (3+)
        if (winStreak >= 3)
        {
            bonus += 5 * winStreak;
        }

        return bonus;
    }

    public static int CalculateTotalCoins(int level, int remainingMoves, string difficulty, bool boostersUsed, bool firstTry, int winStreak)
    {
        int baseCoins = GetBaseCoins(level);
        int coinPerMove = GetCoinPerMove(level);

        float rawCoins = (baseCoins + (remainingMoves * coinPerMove));
        float multiplier = GetDifficultyMultiplier(difficulty);
        
        int coins = Mathf.RoundToInt(rawCoins * multiplier);
        
        // Add bonuses
        // Only turn on bonuses after level 30 as per request (optional, but implemented for safety)
        if (level > 30)
        {
            coins += GetBonusCoins(boostersUsed, firstTry, winStreak);
        }

        // Cap max coins
        int maxCoins = baseCoins * 4;
        
        // Ensure bonus doesn't break the cap too much? 
        // The user said "MaxCoins = BaseCoins * 4". 
        // If bonus pushes it over, should we cap? 
        // "MaxCoins = BaseCoins * 4" usually applies to the base+moves part. 
        // But let's apply it to the final result to be safe against inflation.
        // Or maybe just the multiplier part.
        
        return Mathf.Min(coins, maxCoins);
    }
}
