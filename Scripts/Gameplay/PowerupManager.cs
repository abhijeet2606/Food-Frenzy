using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum PowerupType
{
    None,
    Knife, // Destroy single item
    Oven, // Explosion (3x3)
    Blender, // Shuffle Board
    Pan, // Destroy Row
    Hat // Color Bomb (Destroy all of same color)
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

        // Ensure we only execute on valid FoodItems
        if (target.GetComponent<FoodItem>() == null) return;

        switch (currentActivePowerup)
        {
            case PowerupType.Knife:
                boardManager.ApplyKnifePowerup(target);
                break;
            case PowerupType.Oven:
                boardManager.ApplyOvenPowerup(target);
                break;
            case PowerupType.Blender:
                boardManager.ApplyBlenderPowerup();
                break;
            case PowerupType.Pan:
                boardManager.ApplyPanPowerup(target);
                break;
            case PowerupType.Hat:
                boardManager.ApplyHatPowerup(target);
                break;
        }

        DeselectPowerup();
    }
}
