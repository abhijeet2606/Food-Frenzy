using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class CoinShowerEffect : MonoBehaviour
{
    [Header("UI References")]
    public GameObject CoinPrefab; // Assign a UI Image prefab
    public Transform TargetContainer; // Where coins spawn/move within (usually the panel)
    public Transform Destination; // The coin counter UI element
    public Text CounterText; // Text to update

    [Header("Settings")]
    public int MaxCoinsToSpawn = 20;
    public float Duration = 1.0f;
    [Range(0.1f, 3f)] public float CoinScale = 0.8f; // Scale of the coin icons
    public float Spread = 100f; // Random spread radius
    public bool SpawnFromBottom = false; // Disabled by default, uses TargetContainer as spawn point
    
    // Additional setting for user flexibility
    public bool PunchTextEffect = false; // Disable text punch effect by default

    private GameObject _defaultCoinPrefab;

    public void PlayShower(int totalCoinsEarned, int currentTotalCoins = 0)
    {
        // 1. Determine how many visual coins to spawn (cap it so we don't lag)
        int visualCoins = Mathf.Min(totalCoinsEarned, MaxCoinsToSpawn);
        if (visualCoins < 5 && totalCoinsEarned > 0) visualCoins = 5; // Min visual coins
        if (totalCoinsEarned == 0) visualCoins = 0;

        // 2. Start coroutine
        StartCoroutine(SpawnCoinsRoutine(visualCoins, totalCoinsEarned, currentTotalCoins));
    }

    private System.Collections.IEnumerator SpawnCoinsRoutine(int visualCoins, int earnedValue, int startValue)
    {
        int currentDisplayed = startValue;
        if (CounterText != null) CounterText.text = currentDisplayed.ToString();
        
        // Calculate value per coin
        int valuePerCoin = visualCoins > 0 ? earnedValue / visualCoins : 0;
        int remainder = visualCoins > 0 ? earnedValue % visualCoins : 0;

        // Calculate Bottom Center Position relative to screen
        Vector3 bottomPos = new Vector3(Screen.width / 2f, Screen.height * 0.15f, 0); // 15% from bottom
        if (TargetContainer != null)
        {
            // If using a container, try to find its bottom edge
            var rect = TargetContainer.GetComponent<RectTransform>();
            if (rect != null)
            {
                // World position of bottom center? Hard to get exactly without corners.
                // Simplified: Just use the container's position minus half height
                 bottomPos = TargetContainer.position - new Vector3(0, Screen.height * 0.3f, 0); // Roughly shift down
            }
        }

        for (int i = 0; i < visualCoins; i++)
        {
            int index = i;
            int addValue = valuePerCoin + (index == visualCoins - 1 ? remainder : 0);

            // Spawn
            GameObject coin = CreateCoin();
            if (coin == null) continue;

            // Start Position logic
            Vector3 startPos;
            if (SpawnFromBottom)
            {
                 // Spawn at the "down below" area
                 startPos = bottomPos;
            }
            else
            {
                 // Original Center logic
                 startPos = TargetContainer != null ? TargetContainer.position : transform.position;
            }

            // Add some randomness to start position
            startPos += (Vector3)Random.insideUnitCircle * Spread;

            coin.transform.position = startPos;
            coin.transform.localScale = Vector3.zero;

            // Destination Position
            Vector3 destPos = Destination != null ? Destination.position : startPos + Vector3.up * 800f;

            // Animate
            Sequence seq = DOTween.Sequence();
            
            // Pop in
            seq.Append(coin.transform.DOScale(CoinScale, 0.3f).SetEase(Ease.OutBack));
            
            // Wait a bit random
            seq.AppendInterval(Random.Range(0f, 0.5f));
            
            // Move to destination
            seq.Append(coin.transform.DOMove(destPos, Duration).SetEase(Ease.InCubic));
            
            // On Complete
            seq.OnComplete(() => {
                currentDisplayed += addValue;
                
                // Update Text
                if (CounterText != null)
                {
                    CounterText.text = currentDisplayed.ToString();
                    if (PunchTextEffect)
                    {
                        CounterText.transform.DOPunchScale(Vector3.one * 0.2f, 0.1f);
                    }
                }
                
                Destroy(coin);
            });
            
            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
        }
    }

    private GameObject CreateCoin()
    {
        if (CoinPrefab != null)
        {
            try 
            {
                return Instantiate(CoinPrefab, TargetContainer != null ? TargetContainer : transform);
            }
            catch
            {
                Debug.LogWarning("CoinShowerEffect: CoinPrefab is assigned but invalid/missing. Using fallback.");
            }
        }
        
        // Fallback: Create a simple yellow circle sprite
        if (_defaultCoinPrefab == null)
        {
            _defaultCoinPrefab = new GameObject("DefaultCoin");
            var img = _defaultCoinPrefab.AddComponent<Image>();
            // Try to find a circle sprite or just use a square
            img.color = Color.yellow;
            _defaultCoinPrefab.AddComponent<RectTransform>().sizeDelta = new Vector2(50, 50);
        }
        
        return Instantiate(_defaultCoinPrefab, TargetContainer != null ? TargetContainer : transform);
    }
}
