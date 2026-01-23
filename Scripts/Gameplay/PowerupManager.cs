using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum PowerupType
{
    None,
    Knife, // Destroy single item
    // Add more types as needed
}

public class PowerupManager : MonoBehaviour
{
    public BoardManager boardManager;
    public PowerupType currentActivePowerup = PowerupType.None;

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
            case PowerupType.Knife:
                boardManager.ApplyKnifePowerup(target);
                break;
        }

        DeselectPowerup();
    }
}
