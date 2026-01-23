using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    public Text DebugText;
    public bool ShowDebugInfo = false;

    public GridSystem grid;
    
    [Header("Game Settings")]
    public int maxMoves = 20;
    private int currentMoves;
    private int score;
    private bool isGameOver = false;

    [Header("References")]
    public UIManager uiManager;
    public PowerupManager powerupManager;
    public SoundManager soundManager;

    [Header("Prefabs")]
    public GameObject[] FoodPrefabs;
    public GameObject[] ExplosionPrefabs;
    public GameObject[] BonusPrefabs;

    [Header("Level Goals")]
    public List<LevelGoal> levelGoals;

    // Board Constants
public Vector2 CandySize = new Vector2(1.7f, 1.7f);
public Vector2 BottomRight = new Vector2(-3.5f, -5.8f);


    private GameState state = GameState.None;
    private GameObject hitGo = null;
    private Vector2[] SpawnPositions;

    private Vector2 firstTouchPosition;
    private Vector2 finalTouchPosition;
    private float swipeAngle = 0;
    private float swipeResist = 0.5f;

    private IEnumerator CheckPotentialMatchesCoroutine;
    private IEnumerator AnimatePotentialMatchesCoroutine;
    IEnumerable<GameObject> potentialMatches;

    void Awake()
    {
        if (DebugText != null) DebugText.enabled = ShowDebugInfo;
    }

    void Start()
    {
        InitializeTypesOnPrefabShapesAndBonuses();
        SetupLevelGoals();
        InitializeBoardAndSpawnPositions();
        StartCheckForPotentialMatches();
    }

    private void SetupLevelGoals()
    {
        maxMoves = 20;
        currentMoves = maxMoves;

        // Ensure levelGoals is initialized
        if (levelGoals == null)
        {
            levelGoals = new List<LevelGoal>();
        }

        // Helper to find prefab name based on partial string
        string FindPrefabName(string partialName)
        {
            foreach (var prefab in FoodPrefabs)
            {
                if (prefab.name.ToLower().Contains(partialName.ToLower()))
                    return prefab.name;
            }
            return partialName; // Fallback to the partial name if not found
        }

        // Define the missions: 15 burgers, 15 tomatoes, 10 cheese
        var missions = new (string name, int amount)[]
        {
            ("burger", 15),
            ("tomato", 15),
            ("cheese", 10)
        };

        // Update existing goals or add new ones
        for (int i = 0; i < missions.Length; i++)
        {
            string targetName = FindPrefabName(missions[i].name);
            int amount = missions[i].amount;

            if (i < levelGoals.Count)
            {
                // Update existing goal
                levelGoals[i].TargetPrefabName = targetName;
                levelGoals[i].AmountNeeded = amount;
                levelGoals[i].AmountCollected = 0;
            }
            else
            {
                // Add new goal (Note: UI references like GoalCountText will be null and need Inspector assignment)
                levelGoals.Add(new LevelGoal
                {
                    TargetPrefabName = targetName,
                    AmountNeeded = amount,
                    AmountCollected = 0
                });
            }
        }
        
        // Remove extra goals if any
        if (levelGoals.Count > missions.Length)
        {
            levelGoals.RemoveRange(missions.Length, levelGoals.Count - missions.Length);
        }
    }

    private void InitializeTypesOnPrefabShapesAndBonuses()
    {
        foreach (var item in FoodPrefabs)
        {
            item.GetComponent<FoodItem>().Type = item.name;
        }

        foreach (var item in BonusPrefabs)
        {
            item.GetComponent<FoodItem>().Type = FoodPrefabs.
                Where(x => x.GetComponent<FoodItem>().Type.Contains(item.name.Split('_')[1].Trim())).Single().name;
        }
    }

    public void InitializeBoardAndSpawnPositions()
    {
        InitializeVariables();

        if (grid != null)
            DestroyAllFood();

        grid = new GridSystem();
        SpawnPositions = new Vector2[GameConstants.Columns];

        for (int row = 0; row < GameConstants.Rows; row++)
        {
            for (int column = 0; column < GameConstants.Columns; column++)
            {
                GameObject newFood = GetRandomFood();

                // Prevent initial matches
                while (column >= 2 && grid[row, column - 1].GetComponent<FoodItem>()
                    .IsSameType(newFood.GetComponent<FoodItem>())
                    && grid[row, column - 2].GetComponent<FoodItem>().IsSameType(newFood.GetComponent<FoodItem>()))
                {
                    newFood = GetRandomFood();
                }

                while (row >= 2 && grid[row - 1, column].GetComponent<FoodItem>()
                    .IsSameType(newFood.GetComponent<FoodItem>())
                    && grid[row - 2, column].GetComponent<FoodItem>().IsSameType(newFood.GetComponent<FoodItem>()))
                {
                    newFood = GetRandomFood();
                }

                InstantiateAndPlaceNewFood(row, column, newFood);
            }
        }

        SetupSpawnPositions();
    }

    private void InitializeVariables()
    {
        score = 0;
        currentMoves = maxMoves;
        isGameOver = false;

        if (levelGoals != null)
        {
            foreach (var goal in levelGoals)
            {
                goal.AmountCollected = 0;
                if (uiManager != null) uiManager.UpdateGoalUI(goal);
            }
        }

        if (uiManager != null) uiManager.UpdateMoves(currentMoves);
        ShowScore();
    }

    private void InstantiateAndPlaceNewFood(int row, int column, GameObject newFood)
    {
        GameObject go = Instantiate(newFood,
            BottomRight + new Vector2(column * CandySize.x, row * CandySize.y), Quaternion.identity)
            as GameObject;

        go.GetComponent<FoodItem>().Assign(newFood.GetComponent<FoodItem>().Type, row, column);
        grid[row, column] = go;
    }

    private void SetupSpawnPositions()
    {
        for (int column = 0; column < GameConstants.Columns; column++)
        {
            SpawnPositions[column] = BottomRight
                + new Vector2(column * CandySize.x, GameConstants.Rows * CandySize.y);
        }
    }

    private void DestroyAllFood()
    {
        for (int row = 0; row < GameConstants.Rows; row++)
        {
            for (int column = 0; column < GameConstants.Columns; column++)
            {
                Destroy(grid[row, column]);
            }
        }
    }

    void Update()
    {
        if (state == GameState.None)
        {
            // Check powerup first
            if (powerupManager != null && powerupManager.IsPowerupActive() && Input.GetMouseButtonDown(0))
            {
                var hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
                if (hit.collider != null)
                {
                    powerupManager.TryExecutePowerup(hit.collider.gameObject);
                    return;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                var hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
                if (hit.collider != null)
                {
                    hitGo = hit.collider.gameObject;
                    firstTouchPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    state = GameState.SelectionStarted;
                }
            }
        }
        else if (state == GameState.SelectionStarted)
        {
            if (Input.GetMouseButton(0))
            {
                finalTouchPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if (Vector2.Distance(firstTouchPosition, finalTouchPosition) > swipeResist)
                {
                    CalculateAngle();
                    TrySwap();
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                 state = GameState.None;
            }
        }
    }

    private void CalculateAngle()
    {
        swipeAngle = Mathf.Atan2(finalTouchPosition.y - firstTouchPosition.y, finalTouchPosition.x - firstTouchPosition.x) * 180 / Mathf.PI;
    }

    private void TrySwap()
    {
        int row = hitGo.GetComponent<FoodItem>().Row;
        int column = hitGo.GetComponent<FoodItem>().Column;
        GameObject hitGo2 = null;

        if (swipeAngle > -45 && swipeAngle <= 45 && column < GameConstants.Columns - 1)
        {
            // Right Swipe
            hitGo2 = grid[row, column + 1];
        }
        else if (swipeAngle > 45 && swipeAngle <= 135 && row < GameConstants.Rows - 1)
        {
            // Up Swipe
            hitGo2 = grid[row + 1, column];
        }
        else if ((swipeAngle > 135 || swipeAngle <= -135) && column > 0)
        {
            // Left Swipe
            hitGo2 = grid[row, column - 1];
        }
        else if (swipeAngle < -45 && swipeAngle >= -135 && row > 0)
        {
            // Down Swipe
            hitGo2 = grid[row - 1, column];
        }

        if (hitGo2 != null)
        {
            state = GameState.Animating;
            StopCheckForPotentialMatches();
            FixSortingLayer(hitGo, hitGo2);
            StartCoroutine(FindMatchesAndCollapse(hitGo, hitGo2));
        }
        else
        {
            state = GameState.None;
        }
    }

    private void FixSortingLayer(GameObject hitGo, GameObject hitGo2)
    {
        SpriteRenderer sp1 = hitGo.GetComponent<SpriteRenderer>();
        SpriteRenderer sp2 = hitGo2.GetComponent<SpriteRenderer>();
        sp1.sortingOrder = 300;
        sp2.sortingOrder = 300;
        
        // Ensure they are fully visible
        Color c1 = sp1.color;
        c1.a = 1f;
        sp1.color = c1;

        Color c2 = sp2.color;
        c2.a = 1f;
        sp2.color = c2;
    }

    private IEnumerator FindMatchesAndCollapse(GameObject hitGo, GameObject hitGo2)
    {
        grid.Swap(hitGo, hitGo2);

        Vector2 hitGoPos = BottomRight + new Vector2(hitGo.GetComponent<FoodItem>().Column * CandySize.x, hitGo.GetComponent<FoodItem>().Row * CandySize.y);
        Vector2 hitGo2Pos = BottomRight + new Vector2(hitGo2.GetComponent<FoodItem>().Column * CandySize.x, hitGo2.GetComponent<FoodItem>().Row * CandySize.y);

        hitGo.transform.DOMove(hitGoPos, GameConstants.AnimationDuration);
        hitGo2.transform.DOMove(hitGo2Pos, GameConstants.AnimationDuration);
        yield return new WaitForSeconds(GameConstants.AnimationDuration);

        var hitGomatchesInfo = grid.GetMatches(hitGo);
        var hitGo2matchesInfo = grid.GetMatches(hitGo2);

        var totalMatches = hitGomatchesInfo.MatchedFood
            .Union(hitGo2matchesInfo.MatchedFood).Distinct().ToList();

        if (totalMatches.Count < GameConstants.MinimumMatches)
        {
            hitGo.transform.DOMove(hitGo2Pos, GameConstants.AnimationDuration);
            hitGo2.transform.DOMove(hitGoPos, GameConstants.AnimationDuration);
            yield return new WaitForSeconds(GameConstants.AnimationDuration);

            grid.UndoSwap();
            
            // Fix sorting layer back
             hitGo.GetComponent<SpriteRenderer>().sortingOrder = 0;
             hitGo2.GetComponent<SpriteRenderer>().sortingOrder = 0;
        }
        else
        {
            // Valid move
            currentMoves--;
            if (uiManager != null) uiManager.UpdateMoves(currentMoves);
            
             // Reset sorting order for matched items
             hitGo.GetComponent<SpriteRenderer>().sortingOrder = 0;
             hitGo2.GetComponent<SpriteRenderer>().sortingOrder = 0;
        }


        bool addBonus = totalMatches.Count >= GameConstants.MinimumMatchesForBonus &&
            !BonusTypeUtilities.ContainsDestroyWholeRowColumn(hitGomatchesInfo.BonusesContained) &&
            !BonusTypeUtilities.ContainsDestroyWholeRowColumn(hitGo2matchesInfo.BonusesContained);

        FoodItem hitGoCache = null;
        if (addBonus)
        {
            var sameTypeGo = hitGomatchesInfo.MatchedFood.Count() > 0 ? hitGo : hitGo2;
            hitGoCache = sameTypeGo.GetComponent<FoodItem>();
        }

        int timesRun = 1;
        while (totalMatches.Count >= GameConstants.MinimumMatches)
        {
            IncreaseScore((totalMatches.Count - 2) * GameConstants.Match3Score);

            if (timesRun >= 2)
                IncreaseScore(GameConstants.SubsequentMatchScore);

            if (soundManager != null) soundManager.PlayCrincle();

            // Calculate columns BEFORE destroying items to ensure we have valid data
            var columns = totalMatches.Select(go => go.GetComponent<FoodItem>().Column).Distinct().ToList();

            foreach (var item in totalMatches)
            {
                // Check goals
                if (levelGoals != null)
                {
                    var shape = item.GetComponent<FoodItem>();
                    foreach (var goal in levelGoals)
                    {
                        if (shape.Type.Contains(goal.TargetPrefabName))
                        {
                            goal.AmountCollected++;
                            if (uiManager != null) uiManager.UpdateGoalUI(goal);
                        }
                    }
                }

                grid.Remove(item);
                RemoveFromScene(item);
            }

            // Check for Win
            if (CheckWinCondition())
            {
                isGameOver = true;
                if (uiManager != null) uiManager.ShowWin();
                state = GameState.Win;
                yield break;
            }

            if (addBonus)
                CreateBonus(hitGoCache);

            addBonus = false;

            var collapsedCandyInfo = grid.Collapse(columns);
            var newCandyInfo = CreateNewCandyInSpecificColumns(columns);

            int maxDistance = Mathf.Max(collapsedCandyInfo.MaxDistance, newCandyInfo.MaxDistance);

            MoveAndAnimate(newCandyInfo.AlteredFood, maxDistance);
            MoveAndAnimate(collapsedCandyInfo.AlteredFood, maxDistance);

            yield return new WaitForSeconds(GameConstants.MoveAnimationMinDuration * maxDistance);

            totalMatches = grid.GetMatches(collapsedCandyInfo.AlteredFood).
                Union(grid.GetMatches(newCandyInfo.AlteredFood)).Distinct().ToList();

            timesRun++;
        }

        state = GameState.None;

        if (currentMoves <= 0 && !isGameOver)
        {
            isGameOver = true;
            if (uiManager != null) uiManager.ShowLose();
            state = GameState.Lose;
        }
        else if (!isGameOver)
        {
            StartCheckForPotentialMatches();
        }
    }

    private void CreateBonus(FoodItem hitGoCache)
    {
        GameObject bonusPrefab = GetBonusFromType(hitGoCache.Type);
        if (bonusPrefab == null) return;

        GameObject Bonus = Instantiate(bonusPrefab, BottomRight
            + new Vector2(hitGoCache.Column * CandySize.x,
                hitGoCache.Row * CandySize.y), Quaternion.identity)
            as GameObject;
        grid[hitGoCache.Row, hitGoCache.Column] = Bonus;
        var BonusShape = Bonus.GetComponent<FoodItem>();
        BonusShape.Assign(hitGoCache.Type, hitGoCache.Row, hitGoCache.Column);
        BonusShape.Bonus |= BonusType.DestroyWholeRowColumn;
    }

    private AlteredFoodInfo CreateNewCandyInSpecificColumns(IEnumerable<int> columnsWithMissingCandy)
    {
        AlteredFoodInfo newCandyInfo = new AlteredFoodInfo();

        foreach (int column in columnsWithMissingCandy)
        {
            var emptyItems = grid.GetEmptyItemsOnColumn(column);
            foreach (var item in emptyItems)
            {
                var go = GetRandomFood();
                GameObject newFood = Instantiate(go, SpawnPositions[column], Quaternion.identity)
                    as GameObject;

                newFood.GetComponent<FoodItem>().Assign(go.GetComponent<FoodItem>().Type, item.Row, item.Column);

                if (GameConstants.Rows - item.Row > newCandyInfo.MaxDistance)
                    newCandyInfo.MaxDistance = GameConstants.Rows - item.Row;

                grid[item.Row, item.Column] = newFood;
                newCandyInfo.AddFood(newFood);
            }
        }
        return newCandyInfo;
    }

    private void MoveAndAnimate(IEnumerable<GameObject> movedGameObjects, int distance)
    {
        foreach (var item in movedGameObjects)
        {
            item.transform.DOMove(
                BottomRight + new Vector2(item.GetComponent<FoodItem>().Column * CandySize.x,
                    item.GetComponent<FoodItem>().Row * CandySize.y),
                GameConstants.MoveAnimationMinDuration * distance
            );
        }
    }

    private void RemoveFromScene(GameObject item)
    {
        GameObject explosion = GetRandomExplosion();
        if (explosion != null)
        {
            var newExplosion = Instantiate(explosion, item.transform.position, Quaternion.identity) as GameObject;
            Destroy(newExplosion, GameConstants.ExplosionDuration);
        }
        Destroy(item);
    }

    private GameObject GetRandomFood()
    {
        return FoodPrefabs[Random.Range(0, FoodPrefabs.Length)];
    }

    private void IncreaseScore(int amount)
    {
        score += amount;
        ShowScore();
    }

    private void ShowScore()
    {
        if (uiManager != null) uiManager.UpdateScore(score);
    }

    private GameObject GetRandomExplosion()
    {
        if (ExplosionPrefabs == null || ExplosionPrefabs.Length == 0)
        {
            return null;
        }
        return ExplosionPrefabs[Random.Range(0, ExplosionPrefabs.Length)];
    }

    private GameObject GetBonusFromType(string type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return null;
        }

        foreach (var item in BonusPrefabs)
        {
            if (item.GetComponent<FoodItem>().Type.Contains(type))
                return item;
        }
        return null;
    }

    private void StartCheckForPotentialMatches()
    {
        StopCheckForPotentialMatches();
        CheckPotentialMatchesCoroutine = CheckPotentialMatches();
        StartCoroutine(CheckPotentialMatchesCoroutine);
    }

    private void StopCheckForPotentialMatches()
    {
        if (AnimatePotentialMatchesCoroutine != null)
            StopCoroutine(AnimatePotentialMatchesCoroutine);
        if (CheckPotentialMatchesCoroutine != null)
            StopCoroutine(CheckPotentialMatchesCoroutine);
        ResetOpacityOnPotentialMatches();
    }

    private void ResetOpacityOnPotentialMatches()
    {
        if (potentialMatches != null)
            foreach (var item in potentialMatches)
            {
                if (item == null) break;

                Color c = item.GetComponent<SpriteRenderer>().color;
                c.a = 1.0f;
                item.GetComponent<SpriteRenderer>().color = c;
            }
    }

    private IEnumerator CheckPotentialMatches()
    {
        yield return new WaitForSeconds(GameConstants.WaitBeforePotentialMatchesCheck);
        potentialMatches = MatchUtility.GetPotentialMatches(grid);
        if (potentialMatches != null)
        {
            while (true)
            {
                AnimatePotentialMatchesCoroutine = MatchUtility.AnimatePotentialMatches(potentialMatches);
                StartCoroutine(AnimatePotentialMatchesCoroutine);
                yield return new WaitForSeconds(GameConstants.WaitBeforePotentialMatchesCheck);
            }
        }
    }

    private bool CheckWinCondition()
    {
        if (levelGoals == null || levelGoals.Count == 0) return false;
        foreach (var goal in levelGoals)
        {
            if (!goal.IsComplete()) return false;
        }
        return true;
    }

    public void ApplyKnifePowerup(GameObject item)
    {
        StartCoroutine(DestroySingleItemRoutine(item));
    }

    private IEnumerator DestroySingleItemRoutine(GameObject item)
    {
        state = GameState.Animating;

        var shape = item.GetComponent<FoodItem>();
        int col = shape.Column;
        
        grid.Remove(item);
        RemoveFromScene(item);

        yield return new WaitForSeconds(GameConstants.ExplosionDuration);

        var columns = new List<int> { col };

        var collapsedCandyInfo = grid.Collapse(columns);
        var newCandyInfo = CreateNewCandyInSpecificColumns(columns);

        int maxDistance = Mathf.Max(collapsedCandyInfo.MaxDistance, newCandyInfo.MaxDistance);

        MoveAndAnimate(newCandyInfo.AlteredFood, maxDistance);
        MoveAndAnimate(collapsedCandyInfo.AlteredFood, maxDistance);

        yield return new WaitForSeconds(GameConstants.MoveAnimationMinDuration * maxDistance);

        state = GameState.None;
        StartCheckForPotentialMatches();
    }
}
