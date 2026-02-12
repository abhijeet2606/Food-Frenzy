using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum PowerupType
{
    None,
    HorizontalKnife, // Removes entire line horizontally
    VerticalKnife,   // Removes entire line vertically
    Pan,             // Triggers a 3x3 explosion area
    Oven,            // Acts as a Color Bomb - destroys all items of one color
    Flies,           // The remote target bonus
    Blender,         // Shuffle Board
}

public class PowerupManager : MonoBehaviour
{
    public BoardManager boardManager;
    public PowerupType currentActivePowerup = PowerupType.None;
    private float deselectBlockWindow = 0.08f;
    private float deselectBlockEndTime = 0f;

    void Awake()
    {
        currentActivePowerup = PowerupType.None;
    }

    public void SelectPowerup(string typeName)
    {
        if (Time.time < deselectBlockEndTime) return;

        if (boardManager == null)
        {
            Debug.LogError("BoardManager not assigned in PowerupManager");
            return;
        }

        if (!System.Enum.TryParse(typeName, ignoreCase: true, out PowerupType type))
        {
            currentActivePowerup = PowerupType.None;
            return;
        }

        if (type != PowerupType.None && GetLocalPowerupCount(type) <= 0)
        {
            currentActivePowerup = PowerupType.None;
            return;
        }

        currentActivePowerup = (currentActivePowerup == type) ? PowerupType.None : type;
    }

    public void DeselectPowerup()
    {
        currentActivePowerup = PowerupType.None;
        deselectBlockEndTime = Time.time + deselectBlockWindow;
    }

    public bool IsPowerupActive()
    {
        return currentActivePowerup != PowerupType.None;
    }

    public void TryExecutePowerup(GameObject target)
    {
        if (target == null || !IsPowerupActive()) return;
        if (Time.time < deselectBlockEndTime) return;

        if (GetLocalPowerupCount(currentActivePowerup) <= 0)
        {
            DeselectPowerup();
            return;
        }

        switch (currentActivePowerup)
        {
            case PowerupType.HorizontalKnife:
                boardManager.ApplyHorizontalKnifePowerup(target);
                break;
            case PowerupType.VerticalKnife:
                boardManager.ApplyVerticalKnifePowerup(target);
                break;
            case PowerupType.Pan:
                boardManager.ApplyPanPowerup(target);
                break;
            case PowerupType.Oven:
                boardManager.ApplyOvenPowerup(target);
                break;
            case PowerupType.Flies:
                boardManager.ApplyFliesPowerup(target);
                break;
            case PowerupType.Blender:
                boardManager.ApplyBlenderPowerup();
                break;
        }

        ConsumeLocalPowerup(currentActivePowerup, 1);
        DeselectPowerup();
    }

    private static int GetLocalPowerupCount(PowerupType type)
    {
        string key = GetPowerupKey(type);
        if (string.IsNullOrEmpty(key)) return int.MaxValue;
        return ProgressDataManager.EnsureInstance().GetPowerupCount(key);
    }

    private static void ConsumeLocalPowerup(PowerupType type, int amount)
    {
        string key = GetPowerupKey(type);
        if (string.IsNullOrEmpty(key)) return;
        ProgressDataManager.EnsureInstance().ConsumePowerup(key, amount);
    }

    private static string GetPowerupKey(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.HorizontalKnife:
                return "Powerup_HorizontalKnife";
            case PowerupType.VerticalKnife:
                return "Powerup_VerticalKnife";
            case PowerupType.Pan:
                return "Powerup_Pan";
            case PowerupType.Oven:
                return "Powerup_Oven";
            case PowerupType.Flies:
                return "Powerup_Flies";
            case PowerupType.Blender:
                return "Powerup_Blender";
            default:
                return null;
        }
    }
}
