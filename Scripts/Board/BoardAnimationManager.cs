using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

public class BoardAnimationManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject[] ExplosionPrefabs;

    [Header("Animation Settings")]
    public float waveStepDelay = 0.2f;
    public float wavePopupDuration = 0.2f;
    public float ovenStepDelay = 0.18f;
    
    // Regular destruction settings
    public float regularFadeDuration = 0.2f;
    public float regularScaleDuration = 0.2f;

    // Powerup destruction setting
    public float powerupPopupScale = 1.25f;
    public float powerupPopupDuration = 0.2f;
    public float powerupVanishDuration = 0.26f;

    public float AnimateDestruction(List<GameObject> items, GameObject centerItem = null)
    {
        if (items == null || items.Count == 0) return 0f;

        items = items.Where(i => i != null).ToList();
        if (items.Count == 0) return 0f;

        bool isPowerup = centerItem != null;

        // If it's a regular match (no centerItem/source), just do simple simultaneous destruction
        if (!isPowerup)
        {
            foreach (var item in items)
            {
                AnimateSingleItem(item, 0f, 0f, false);
            }
            return 0f; // No extra wait time needed for simple matches
        }

        // --- Logic for Powerups (Wave/Oven) ---

        bool isRow = items.Select(i => i.GetComponent<FoodItem>().Row).Distinct().Count() == 1;
        bool isColumn = items.Select(i => i.GetComponent<FoodItem>().Column).Distinct().Count() == 1;
        bool isLinear = isRow || isColumn;
        
        var centerFood = centerItem.GetComponent<FoodItem>();
        bool isColorBomb = BonusTypeUtilities.ContainsColorBomb(centerFood.Bonus);

        // Sort items
        items = items.OrderBy(i => 
        {
            if (isLinear)
            {
                if (isRow) return Mathf.Abs(i.GetComponent<FoodItem>().Column - centerFood.Column);
                return Mathf.Abs(i.GetComponent<FoodItem>().Row - centerFood.Row);
            }
            
            if (isColorBomb)
            {
                return Vector2.Distance(i.transform.position, centerItem.transform.position);
            }

            return Mathf.Max(Mathf.Abs(i.GetComponent<FoodItem>().Column - centerFood.Column),
                             Mathf.Abs(i.GetComponent<FoodItem>().Row - centerFood.Row));
        }).ToList();

        float stepDelay = waveStepDelay;
        if (isColorBomb) stepDelay = ovenStepDelay;

        float maxDelay = 0f;
        float maxFinishTime = 0f;
        Dictionary<GameObject, float> itemDelays = new Dictionary<GameObject, float>();

        for (int idx = 0; idx < items.Count; idx++)
        {
            var item = items[idx];
            float delay = 0f;

            if (isColorBomb)
            {
                // Oven: Sequential
                delay = stepDelay * idx;
            }
            else
            {
                // Wave/Linear
                int dist = 0;
                if (isLinear)
                {
                    if (isRow) dist = Mathf.Abs(item.GetComponent<FoodItem>().Column - centerFood.Column);
                    else dist = Mathf.Abs(item.GetComponent<FoodItem>().Row - centerFood.Row);
                }
                else
                {
                    dist = Mathf.Max(Mathf.Abs(item.GetComponent<FoodItem>().Column - centerFood.Column),
                                     Mathf.Abs(item.GetComponent<FoodItem>().Row - centerFood.Row));
                }
                delay = stepDelay * dist;
            }

            itemDelays[item] = delay;
            float finishTime = delay + powerupPopupDuration;
            if (finishTime > maxFinishTime) maxFinishTime = finishTime;
            if (delay > maxDelay) maxDelay = delay;
        }

        foreach (var item in items)
        {
            float delay = itemDelays[item];
            float holdTime = 0f;

            // Synchronize vanish for Oven or Non-Linear powerups
            if (!isLinear || isColorBomb)
            {
                holdTime = maxFinishTime - (delay + powerupPopupDuration);
            }

            AnimateSingleItem(item, delay, holdTime, true);
        }

        if (!isLinear || isColorBomb)
            return maxFinishTime + powerupVanishDuration;

        if (isLinear && maxDelay < 0.3f)
            return 0.3f;
            
        return maxDelay;
    }

    private void AnimateSingleItem(GameObject item, float delay, float holdTime, bool isPowerup)
    {
        var sr = item.GetComponent<SpriteRenderer>();
        var collider = item.GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        Sequence s = DOTween.Sequence();

        if (isPowerup)
        {
            // Powerup Style: Pop up -> Wait -> Vanish
            if (delay > 0f) s.AppendInterval(delay);
            s.Append(item.transform.DOScale(powerupPopupScale, powerupPopupDuration));
            if (holdTime > 0f) s.AppendInterval(holdTime);
            s.Append(item.transform.DOScale(0f, powerupVanishDuration));
            if (sr != null) s.Join(sr.DOFade(0f, powerupVanishDuration));
        }
        else
        {
            // Regular Style: Just shrink/fade immediately
            // No delay, no popup
            s.Append(item.transform.DOScale(0f, regularScaleDuration));
            if (sr != null) s.Join(sr.DOFade(0f, regularFadeDuration));
        }

        s.OnComplete(() =>
        {
            if (isPowerup)
            {
                GameObject explosion = GetRandomExplosion();
                if (explosion != null)
                {
                    var newExplosion = Instantiate(explosion, item.transform.position, Quaternion.identity) as GameObject;
                    Destroy(newExplosion, GameConstants.ExplosionDuration);
                }
            }
            Destroy(item);
        });
    }

    private GameObject GetRandomExplosion()
    {
        if (ExplosionPrefabs == null || ExplosionPrefabs.Length == 0) return null;
        return ExplosionPrefabs[Random.Range(0, ExplosionPrefabs.Length)];
    }
}
