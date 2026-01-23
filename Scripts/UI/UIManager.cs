using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public Text MovesText;
    public Text ScoreText;
    public GameObject WinPanel;
    public GameObject LosePanel;
    
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
