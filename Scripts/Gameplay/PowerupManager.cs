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

    void Awake()
    {
        currentActivePowerup = PowerupType.None;
    }

    public void SelectPowerup(string typeName)
    {
        if (boardManager == null)
        {
            Debug.LogError("BoardManager not assigned in PowerupManager");
            return;
        }

        try
        {
            PowerupType type = (PowerupType)System.Enum.Parse(typeof(PowerupType), typeName);
            
            if (currentActivePowerup == type)
            {
                // Toggle off if already selected
                currentActivePowerup = PowerupType.None;
            }
            else
            {
                currentActivePowerup = type;
            }
        }
        catch
        {
            currentActivePowerup = PowerupType.None;
        }
    }

    public void DeselectPowerup()
    {
        currentActivePowerup = PowerupType.None;
    }

    public bool IsPowerupActive()
    {
        return currentActivePowerup != PowerupType.None;
    }

    public void TryExecutePowerup(GameObject target)
    {
        if (target == null || !IsPowerupActive()) return;

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

        DeselectPowerup();
    }
}
