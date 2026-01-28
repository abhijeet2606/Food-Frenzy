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
    private List<GameObject> activeFoodPrefabs;

    // Board Constants
    public Vector2 CandySize = new Vector2(1.7f, 1.7f);
    public Vector2 BottomRight = new Vector2(-3.5f, -5.8f);
    
    // Offset to keep the board centered relative to the screen/UI
    public Vector2 BoardCenterOffset = new Vector2(0f, -1.0f);


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
        SetupLevel();
        if (uiManager != null) uiManager.SetupGoals(levelGoals, FoodPrefabs);
        InitializeBoardAndSpawnPositions();
        StartCheckForPotentialMatches();
    }

    private void SetupLevel()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        LevelData data = LevelManager.Instance.LoadLevel(currentLevel);

        if (levelGoals == null) levelGoals = new List<LevelGoal>();
        else levelGoals.Clear();

        // Helper to find prefab name based on partial string
        string FindPrefabName(string partialName)
        {
            foreach (var prefab in FoodPrefabs)
            {
                if (prefab.name.ToLower().Contains(partialName.ToLower()))
                    return prefab.name;
            }
            return partialName; 
        }

        if (data != null)
        {
            GameConstants.Rows = data.rows;
            GameConstants.Columns = data.columns;
            maxMoves = data.moves;
            currentMoves = maxMoves;

            // Setup Food Types
            activeFoodPrefabs = new List<GameObject>();
            foreach (string type in data.foodTypes)
            {
                foreach (var prefab in FoodPrefabs)
                {
                    if (prefab.name.ToLower().Contains(type.ToLower()))
                    {
                        activeFoodPrefabs.Add(prefab);
                        break;
                    }
                }
            }
            if (activeFoodPrefabs.Count == 0) activeFoodPrefabs.AddRange(FoodPrefabs);

            // Setup Goals
            foreach (var goalData in data.goals)
            {
                string targetName = FindPrefabName(goalData.type);
                levelGoals.Add(new LevelGoal
                {
                    TargetPrefabName = targetName,
                    AmountNeeded = goalData.count,
                    AmountCollected = 0
                });
            }

            Debug.Log($"Level {data.levelId} loaded. Size: {data.rows}x{data.columns}, Moves: {data.moves}");
        }
        else
        {
            // Fallback for testing or if file missing
            SetupFallbackLevel(currentLevel);
        }
    }

    private void SetupFallbackLevel(int currentLevel)
    {
        maxMoves = 20 + (currentLevel * 2); 
        currentMoves = maxMoves;
        activeFoodPrefabs = new List<GameObject>(FoodPrefabs);
        
        // Use default game constants if fallback
        GameConstants.Rows = 6;
        GameConstants.Columns = 6;

        // Helper to find prefab name based on partial string
        string FindPrefabName(string partialName)
        {
            foreach (var prefab in FoodPrefabs)
            {
                if (prefab.name.ToLower().Contains(partialName.ToLower()))
                    return prefab.name;
            }
            return partialName; 
        }

        int baseAmount = 10 + (currentLevel * 2);
        
        var missions = new (string name, int amount)[]
        {
            ("burger", baseAmount),
            ("tomato", baseAmount),
            ("cheese", 10 + currentLevel)
        };

        for (int i = 0; i < missions.Length; i++)
        {
            string targetName = FindPrefabName(missions[i].name);
            int amount = missions[i].amount;

            levelGoals.Add(new LevelGoal
            {
                TargetPrefabName = targetName,
                AmountNeeded = amount,
                AmountCollected = 0
            });
        }
        
        Debug.Log($"Fallback Level {currentLevel} started. Moves: {maxMoves}");
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
        AdjustCameraAndBoardPosition();

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

    private void AdjustCameraAndBoardPosition()
    {
        // Calculate Board Dimensions
        float boardWidth = GameConstants.Columns * CandySize.x;
        float boardHeight = GameConstants.Rows * CandySize.y;

        // Calculate Origin (Bottom-Left Cell Center) based on Center Offset
        BottomRight = new Vector2(
            BoardCenterOffset.x - (boardWidth / 2f) + (CandySize.x / 2f),
            BoardCenterOffset.y - (boardHeight / 2f) + (CandySize.y / 2f)
        );

        // Adjust Camera Size
        if (Camera.main != null)
        {
            // Vertical Fit: Board Height + Padding (Top/Bottom UI space)
            float verticalSize = (boardHeight / 2f) + 3.0f;

            // Horizontal Fit: Board Width + Padding
            float aspect = Camera.main.aspect;
            float horizontalSize = ((boardWidth / 2f) + 1.0f) / aspect;

            Camera.main.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
            
            // Center Camera on X axis
            Vector3 camPos = Camera.main.transform.position;
            camPos.x = BoardCenterOffset.x;
            Camera.main.transform.position = camPos;
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
                if (hit.collider != null && hit.collider.GetComponent<FoodItem>() != null)
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


        BonusType bonusToCreate = BonusType.None;
        FoodItem hitGoCache = null;

        var b1 = grid.AnalyzeMatchShape(hitGo);
        var b2 = grid.AnalyzeMatchShape(hitGo2);

        if (b1 == BonusType.ColorBomb || b2 == BonusType.ColorBomb)
        {
            bonusToCreate = BonusType.ColorBomb;
            hitGoCache = (b1 == BonusType.ColorBomb) ? hitGo.GetComponent<FoodItem>() : hitGo2.GetComponent<FoodItem>();
        }
        else if (b1 == BonusType.Explosion || b2 == BonusType.Explosion)
        {
            bonusToCreate = BonusType.Explosion;
            hitGoCache = (b1 == BonusType.Explosion) ? hitGo.GetComponent<FoodItem>() : hitGo2.GetComponent<FoodItem>();
        }
        else if (b1 == BonusType.DestroyWholeRowColumn || b2 == BonusType.DestroyWholeRowColumn)
        {
            bonusToCreate = BonusType.DestroyWholeRowColumn;
            hitGoCache = (b1 == BonusType.DestroyWholeRowColumn) ? hitGo.GetComponent<FoodItem>() : hitGo2.GetComponent<FoodItem>();
        }

        if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(hitGomatchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsDestroyWholeRowColumn(hitGo2matchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsExplosion(hitGomatchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsExplosion(hitGo2matchesInfo.BonusesContained))
        {
            bonusToCreate = BonusType.None;
        }

        bool addBonus = (bonusToCreate != BonusType.None);

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
                
                // Advance to next level logic
                int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
                PlayerPrefs.SetInt("CurrentLevel", currentLevel + 1);
                Debug.Log($"Level {currentLevel} complete! advancing to Level {currentLevel + 1}");
                
                // Wait a moment then reload (or wait for UI input if we had a next button)
                StartCoroutine(WaitAndReloadScene());
                
                yield break;
            }

            if (addBonus)
                CreateBonus(hitGoCache, bonusToCreate);

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

    private void CreateBonus(FoodItem hitGoCache, BonusType bonusType)
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
        BonusShape.Bonus |= bonusType;
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
        if (activeFoodPrefabs != null && activeFoodPrefabs.Count > 0)
            return activeFoodPrefabs[Random.Range(0, activeFoodPrefabs.Count)];
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

    private void ShuffleBoard()
    {
        if (state != GameState.None) return;
        state = GameState.Animating;

        List<GameObject> allFood = new List<GameObject>();
        for (int row = 0; row < GameConstants.Rows; row++)
        {
            for (int column = 0; column < GameConstants.Columns; column++)
            {
                allFood.Add(grid[row, column]);
            }
        }

        int index = 0;
        for (int row = 0; row < GameConstants.Rows; row++)
        {
            for (int column = 0; column < GameConstants.Columns; column++)
            {
                // Randomly swap with remaining items to ensure randomness
                int swapIndex = UnityEngine.Random.Range(index, allFood.Count);
                GameObject temp = allFood[index];
                allFood[index] = allFood[swapIndex];
                allFood[swapIndex] = temp;
                
                GameObject obj = allFood[index];

                int retryCount = 0;
                while (
                   (column >= 2 && grid[row, column - 1].GetComponent<FoodItem>().IsSameType(obj.GetComponent<FoodItem>()) && grid[row, column - 2].GetComponent<FoodItem>().IsSameType(obj.GetComponent<FoodItem>()))
                   ||
                   (row >= 2 && grid[row - 1, column].GetComponent<FoodItem>().IsSameType(obj.GetComponent<FoodItem>()) && grid[row - 2, column].GetComponent<FoodItem>().IsSameType(obj.GetComponent<FoodItem>()))
                )
                {
                    swapIndex = UnityEngine.Random.Range(index + 1, allFood.Count);
                    if (swapIndex >= allFood.Count) 
                    {
                         break;
                    }

                    temp = allFood[index];
                    allFood[index] = allFood[swapIndex];
                    allFood[swapIndex] = temp;

                    obj = allFood[index];
                    retryCount++;
                    if (retryCount > 100) break;
                }

                grid[row, column] = obj;
                obj.GetComponent<FoodItem>().Row = row;
                obj.GetComponent<FoodItem>().Column = column;
                index++;
            }
        }

        foreach (var item in allFood)
        {
            Vector2 pos = BottomRight + new Vector2(item.GetComponent<FoodItem>().Column * CandySize.x, item.GetComponent<FoodItem>().Row * CandySize.y);
            item.transform.DOMove(pos, 0.5f);
        }

        StartCoroutine(OnShuffleComplete());
    }

    private IEnumerator OnShuffleComplete()
    {
        yield return new WaitForSeconds(0.5f);
        state = GameState.None;
        StartCheckForPotentialMatches();
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
        else
        {
            ShuffleBoard();
        }
    }

    private IEnumerator WaitAndReloadScene()
    {
        yield return new WaitForSeconds(3.0f); // Wait for win animation/celebration
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
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
        StartCoroutine(DestroyItemsRoutine(new List<GameObject> { item }));
    }

    public void ApplyOvenPowerup(GameObject item)
    {
        var shape = item.GetComponent<FoodItem>();
        int row = shape.Row;
        int col = shape.Column;
        List<GameObject> items = new List<GameObject>();
        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                if (r >= 0 && r < GameConstants.Rows && c >= 0 && c < GameConstants.Columns)
                {
                    if (grid[r, c] != null) items.Add(grid[r, c]);
                }
            }
        }
        StartCoroutine(DestroyItemsRoutine(items));
    }

    public void ApplyPanPowerup(GameObject item)
    {
        int row = item.GetComponent<FoodItem>().Row;
        List<GameObject> items = new List<GameObject>();
        for (int col = 0; col < GameConstants.Columns; col++)
        {
            if (grid[row, col] != null) items.Add(grid[row, col]);
        }
        StartCoroutine(DestroyItemsRoutine(items));
    }

    public void ApplyHatPowerup(GameObject item)
    {
        string type = item.GetComponent<FoodItem>().Type;
        List<GameObject> items = new List<GameObject>();
        for (int r = 0; r < GameConstants.Rows; r++)
        {
            for (int c = 0; c < GameConstants.Columns; c++)
            {
                var go = grid[r, c];
                if (go != null && go.GetComponent<FoodItem>().Type == type)
                {
                    items.Add(go);
                }
            }
        }
        StartCoroutine(DestroyItemsRoutine(items));
    }

    public void ApplyBlenderPowerup()
    {
        ShuffleBoard();
    }

    private IEnumerator DestroyItemsRoutine(IEnumerable<GameObject> items)
    {
        state = GameState.Animating;

        List<int> affectedColumns = new List<int>();

        foreach (var item in items)
        {
            if (item == null) continue;
            var shape = item.GetComponent<FoodItem>();
            if (!affectedColumns.Contains(shape.Column))
                affectedColumns.Add(shape.Column);

            grid.Remove(item);
            RemoveFromScene(item);
        }

        yield return new WaitForSeconds(GameConstants.ExplosionDuration);

        var collapsedCandyInfo = grid.Collapse(affectedColumns);
        var newCandyInfo = CreateNewCandyInSpecificColumns(affectedColumns);

        int maxDistance = Mathf.Max(collapsedCandyInfo.MaxDistance, newCandyInfo.MaxDistance);

        MoveAndAnimate(newCandyInfo.AlteredFood, maxDistance);
        MoveAndAnimate(collapsedCandyInfo.AlteredFood, maxDistance);

        yield return new WaitForSeconds(GameConstants.MoveAnimationMinDuration * maxDistance);

        state = GameState.None;
        StartCheckForPotentialMatches();
    }
}
