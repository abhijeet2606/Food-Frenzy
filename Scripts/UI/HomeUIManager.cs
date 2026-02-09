using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeUIManager : MonoBehaviour
{
    public Text LevelText, LevelText2;
    public Text CoinsText;

    // Track selected boosters
    private System.Collections.Generic.List<string> selectedBoosters = new System.Collections.Generic.List<string>();

    private void Start()
    {
        UpdateLevelUI();
        // Reset booster selection on start
        selectedBoosters.Clear();
        PlayerPrefs.DeleteKey("SelectedBoosters");
    }

    private void UpdateLevelUI()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (LevelText != null)
        {
            LevelText.text = "Level " + currentLevel;
        }
        if (LevelText2 != null)
        {
            LevelText2.text = "Level " + currentLevel;
        }

        int totalCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        if (CoinsText != null)
        {
            CoinsText.text = totalCoins.ToString();
        }
    }

    // Called by UI Buttons (Oven, Hat, Knife)
    public void SelectBooster(string boosterName)
    {
        if (selectedBoosters.Contains(boosterName))
        {
            // Deselect if already selected
            selectedBoosters.Remove(boosterName);
            Debug.Log("Booster Deselected: " + boosterName);
        }
        else
        {
            selectedBoosters.Add(boosterName);
            Debug.Log("Booster Selected: " + boosterName);
        }
    }

    public void OnPlayButton()
    {
        // Save selected boosters for the Gameplay scene
        if (selectedBoosters.Count > 0)
        {
            string joinedBoosters = string.Join(",", selectedBoosters);
            PlayerPrefs.SetString("SelectedBoosters", joinedBoosters);
        }
        else
        {
            PlayerPrefs.DeleteKey("SelectedBoosters");
        }

        // Start from Level 1 (or saved level later)
        if (!PlayerPrefs.HasKey("CurrentLevel"))
        {
            PlayerPrefs.SetInt("CurrentLevel", 1);
        }
        SceneManager.LoadScene("Gameplay");
    }

    public void OnResetButton()
    {
        PlayerPrefs.SetInt("CurrentLevel", 1);
        PlayerPrefs.Save();
        UpdateLevelUI();
        Debug.Log("Progress reset to Level 1");
    }
}
