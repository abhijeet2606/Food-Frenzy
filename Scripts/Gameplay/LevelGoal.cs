using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class LevelGoal
{
    public string TargetPrefabName; // The name of the prefab to collect
    public int AmountNeeded;
    
    [HideInInspector]
    public int AmountCollected;
    
    // UI References for this specific goal
    public Image GoalIcon;
    public Text GoalCountText;
    public GameObject CheckmarkObject; // To show when done

    public bool IsComplete()
    {
        return AmountCollected >= AmountNeeded;
    }
}
