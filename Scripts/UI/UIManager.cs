using System;
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
    public Text CoinsText; // Add this field for Gameplay scene coin display
    public GameObject WinPanel;
    public GameObject LosePanel;

    [Header("Effects")]
    public CoinShowerEffect coinShower;

    private int _initialCoins;
    private BoardManager _boardManager;
    private bool _purchaseInProgress;

    [Header("Lose: Extra Moves")]
    public Button LoseBuyMovesButton;
    public Text LoseBuyMovesButtonText;
    public Text LoseExtraMovesAmountText;
    public Text LoseCoinsBalanceText;
    public int ExtraMovesPerPurchase = 5;
    public int MaxExtraMovesPurchasesPerLevel = 3;
    public int FirstExtraMovesCost = 500;
    public int SecondExtraMovesCost = 1200;
    public int SubsequentExtraMovesCostStep = 700;

    private void Start()
    {
        _initialCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        if (CoinsText != null)
        {
            CoinsText.text = _initialCoins.ToString();
        }

        if (LoseBuyMovesButton != null) LoseBuyMovesButton.onClick.AddListener(OnLoseBuyMovesPressed);
    }

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

    public void ShowWin(int coinsEarned = 0)
    {
        int startCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        _initialCoins = startCoins;
        if (CoinsText != null) CoinsText.text = startCoins.ToString();

        if (WinPanel != null) WinPanel.SetActive(true);
        if (coinShower != null && coinsEarned > 0)
        {
            // Delay shower slightly to allow WinPanel popup animation to finish/settle
            DG.Tweening.DOVirtual.DelayedCall(0.5f, () => 
            {
                // Pass the initial coins so it counts up from there
                coinShower.PlayShower(coinsEarned, startCoins);
            });
        }
    }

    public void ShowLose()
    {
        ShowLose(null);
    }

    public void ShowLose(BoardManager boardManager)
    {
        _boardManager = boardManager;
        if (LosePanel != null) LosePanel.SetActive(true);
        RefreshLosePurchaseUI();
    }

    public void HideLose()
    {
        if (LosePanel != null) LosePanel.SetActive(false);
    }

    public void OnLoseBuyMovesPressed()
    {
        if (_purchaseInProgress) return;
        if (_boardManager == null || !_boardManager.IsAwaitingExtraMovesContinue()) return;

        int purchaseIndex = GetExtraMovesPurchasesThisLevel() + 1;
        if (purchaseIndex > Math.Max(0, MaxExtraMovesPurchasesPerLevel)) return;

        int cost = GetExtraMovesCostForPurchase(purchaseIndex);
        int coins = PlayerPrefs.GetInt("TotalCoins", 0);
        if (coins < cost) return;

        _purchaseInProgress = true;

        string spendErr;
        bool ok = ProgressDataManager.EnsureInstance().TrySpendCoins(cost, out spendErr);
        if (!ok)
        {
            _purchaseInProgress = false;
            RefreshLosePurchaseUI();
            return;
        }

        IncrementExtraMovesPurchasesThisLevel();
        RefreshCoinsUI();

        bool continued = _boardManager.ContinueAfterExtraMoves(Math.Max(1, ExtraMovesPerPurchase));
        _purchaseInProgress = false;
        if (!continued) RefreshLosePurchaseUI();
    }

    public void OnQuitButton()
    {
        Time.timeScale = 1f; // Reset time before leaving
        AudioListener.pause = false; // Ensure audio is resumed
        UnityEngine.SceneManagement.SceneManager.LoadScene("Home");
    }

    public void OnWinContinueButton()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Home");
    }

    public void OnLoseContinueButton()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Home");
    }

    private void RefreshCoinsUI()
    {
        int coins = PlayerPrefs.GetInt("TotalCoins", 0);
        _initialCoins = coins;
        if (CoinsText != null) CoinsText.text = coins.ToString();
        if (LoseCoinsBalanceText != null) LoseCoinsBalanceText.text = coins.ToString();
    }

    private void RefreshLosePurchaseUI()
    {
        RefreshCoinsUI();

        int purchaseIndex = GetExtraMovesPurchasesThisLevel() + 1;
        int max = Math.Max(0, MaxExtraMovesPurchasesPerLevel);
        bool canBuyByCount = purchaseIndex <= max;
        int cost = canBuyByCount ? GetExtraMovesCostForPurchase(purchaseIndex) : 0;
        int coins = PlayerPrefs.GetInt("TotalCoins", 0);
        bool canBuyByCoins = coins >= cost && cost > 0;
        bool interactable = canBuyByCount && canBuyByCoins && !_purchaseInProgress && _boardManager != null && _boardManager.IsAwaitingExtraMovesContinue();

        if (LoseExtraMovesAmountText != null) LoseExtraMovesAmountText.text = $"+{Math.Max(1, ExtraMovesPerPurchase)}";
        if (LoseBuyMovesButtonText != null) LoseBuyMovesButtonText.text = $"{cost}";
        if (LoseBuyMovesButton != null) LoseBuyMovesButton.interactable = interactable;
    }

    private int GetExtraMovesCostForPurchase(int purchaseIndex)
    {
        if (purchaseIndex <= 1) return Math.Max(0, FirstExtraMovesCost);
        if (purchaseIndex == 2) return Math.Max(0, SecondExtraMovesCost);

        int step = Math.Max(1, SubsequentExtraMovesCostStep);
        int inc = Math.Max(0, SecondExtraMovesCost - FirstExtraMovesCost);
        int cost = Math.Max(0, SecondExtraMovesCost);

        for (int i = 3; i <= purchaseIndex; i++)
        {
            inc += step;
            cost += inc;
        }

        return cost;
    }

    private static int GetExtraMovesPurchasesThisLevel()
    {
        int level = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (level < 1) level = 1;
        return Math.Max(0, PlayerPrefs.GetInt("Level_" + level + "_ExtraMovesPurchases", 0));
    }

    private static void IncrementExtraMovesPurchasesThisLevel()
    {
        int level = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (level < 1) level = 1;
        string key = "Level_" + level + "_ExtraMovesPurchases";
        int next = Math.Max(0, PlayerPrefs.GetInt(key, 0)) + 1;
        PlayerPrefs.SetInt(key, next);
        PlayerPrefs.Save();
    }
}
