using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public class PreLevelGoalSlot
{
    public GameObject Container;
    public Image Icon;
    public Text LabelText;
}

public class HomeUIManager : MonoBehaviour
{
    public GameObject PreLevelPanel;
    public Text LevelTitleText;
    public PreLevelGoalSlot[] GoalSlots;

    public Toggle OvenToggle;
    public Toggle HatToggle;
    public Toggle KnifeToggle;

    public string GameplaySceneName = "Gameplay";

    public void OnPlayButton()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        LevelData data = LevelManager.Instance.LoadLevel(currentLevel);

        if (PreLevelPanel != null) PreLevelPanel.SetActive(true);
        if (LevelTitleText != null) LevelTitleText.text = "Level " + currentLevel.ToString();

        if (GoalSlots != null)
        {
            foreach (var slot in GoalSlots)
            {
                if (slot != null && slot.Container != null) slot.Container.SetActive(false);
            }

            if (data != null && data.goals != null)
            {
                for (int i = 0; i < data.goals.Length && i < GoalSlots.Length; i++)
                {
                    var g = data.goals[i];
                    var slot = GoalSlots[i];
                    if (slot.Container != null) slot.Container.SetActive(true);
                    if (slot.LabelText != null) slot.LabelText.text = g.type.ToUpper() + " × " + g.count.ToString();
                }
            }
        }

        if (OvenToggle != null) OvenToggle.isOn = false;
        if (HatToggle != null) HatToggle.isOn = false;
        if (KnifeToggle != null) KnifeToggle.isOn = false;
    }

    public void OnPreLevelPlay()
    {
        var list = new List<PowerupType>();
        if (OvenToggle != null && OvenToggle.isOn) list.Add(PowerupType.Oven);
        if (HatToggle != null && HatToggle.isOn) list.Add(PowerupType.Hat);
        if (KnifeToggle != null && KnifeToggle.isOn) list.Add(PowerupType.Knife);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectedBoosters = list;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(GameplaySceneName);
    }
}
