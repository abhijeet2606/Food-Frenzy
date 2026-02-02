using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeUIManager : MonoBehaviour
{
    public Text LevelText, LevelText2;

    private void Start()
    {
        UpdateLevelUI();
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
    }

    public void OnPlayButton()
    {
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
