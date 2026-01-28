using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public class GoalUISlot
{
    public GameObject Container;
    public Image Icon;
    public Text CountText;
    public GameObject Checkmark;
}

public class UIManager : MonoBehaviour
{
    public Text MovesText;
    public Text ScoreText;
    public GameObject WinPanel;
    public GameObject LosePanel;

    [Header("Goal UI Slots")]
    public GoalUISlot[] GoalSlots; // Ensure you have 3 elements here in Inspector
    
    public void SetupGoals(List<LevelGoal> goals, GameObject[] foodPrefabs)
    {
        // 1. Reset all slots first (Hide them)
        if (GoalSlots != null)
        {
            foreach (var slot in GoalSlots)
            {
                if (slot.Container != null) slot.Container.SetActive(false);
            }

            // 2. Loop through the goals for this level
            for (int i = 0; i < goals.Count; i++)
            {
                // Safety check: Don't exceed available UI slots (max 3)
                if (i >= GoalSlots.Length) break;

                var goal = goals[i];
                var slot = GoalSlots[i];

                // Show this slot
                if (slot.Container != null) slot.Container.SetActive(true);

                // A. Set the Image (Find sprite from prefabs)
                if (slot.Icon != null)
                {
                    Sprite iconSprite = null;
                    foreach (var prefab in foodPrefabs)
                    {
                        // Match prefab name (e.g. "Food_Burger")
                        if (prefab.name == goal.TargetPrefabName)
                        {
                            var renderer = prefab.GetComponent<SpriteRenderer>();
                            if (renderer != null) iconSprite = renderer.sprite;
                            break;
                        }
                    }
                    
                    if (iconSprite != null)
                    {
                        slot.Icon.sprite = iconSprite;
                        slot.Icon.enabled = true;
                        
                        // Fix alpha if it was hidden
                        Color c = slot.Icon.color;
                        c.a = 1f;
                        slot.Icon.color = c;
                    }
                }

                // B. Set the Text (Initial Amount)
                if (slot.CountText != null)
                {
                    slot.CountText.text = goal.AmountNeeded.ToString();
                    slot.CountText.gameObject.SetActive(true);
                }

                // C. Reset Checkmark
                if (slot.Checkmark != null)
                {
                    slot.Checkmark.SetActive(false);
                }

                // D. Link UI back to Goal Logic (So updates work automatically)
                goal.GoalIcon = slot.Icon;
                goal.GoalCountText = slot.CountText;
                goal.CheckmarkObject = slot.Checkmark;
            }
        }
    }
    
    public void UpdateMoves(int moves)
    {
        if (MovesText != null)
        {
            MovesText.text = moves.ToString();
        }
    }

    public void UpdateScore(int score)
    {
        if (ScoreText != null)
        {
            ScoreText.text = "Score: " + score;
        }
    }

    public void UpdateGoalUI(LevelGoal goal)
    {
        int remaining = goal.AmountNeeded - goal.AmountCollected;
        if (remaining < 0) remaining = 0;

        if (goal.GoalCountText != null)
        {
            goal.GoalCountText.text = remaining.ToString();
        }

        if (goal.CheckmarkObject != null)
        {
            goal.CheckmarkObject.SetActive(remaining == 0);
            if (remaining == 0 && goal.GoalCountText != null)
            {
                goal.GoalCountText.gameObject.SetActive(false);
            }
        }
    }

    public void ShowWin()
    {
        if (WinPanel != null) WinPanel.SetActive(true);
    }

    public void ShowLose()
    {
        if (LosePanel != null) LosePanel.SetActive(true);
    }
}
