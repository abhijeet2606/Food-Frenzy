using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BoardManager : MonoBehaviour
{
    public Text DebugText;
    public bool ShowDebugInfo = false;
    
    [Header("Debug")]
    public bool DebugWin = false; // Toggle this in Inspector to win instantly

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
    public BoardAnimationManager animationManager;

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
    
    // Level Data Tracking
    private string currentDifficulty = "Normal";
    private bool boostersUsed = false;

    private Vector2 firstTouchPosition;
    private Vector2 finalTouchPosition;
    private float swipeAngle = 0;
    private float swipeResist = 0.2f;

    private IEnumerator CheckPotentialMatchesCoroutine;
    private IEnumerator AnimatePotentialMatchesCoroutine;
    IEnumerable<GameObject> potentialMatches;

    void Awake()
    {
        if (DebugText != null) DebugText.enabled = ShowDebugInfo;
        if (animationManager == null) animationManager = GetComponent<BoardAnimationManager>();
        if (animationManager == null) animationManager = gameObject.AddComponent<BoardAnimationManager>();
        
        // Pass explosion prefabs to animation manager if not set
        if (animationManager.ExplosionPrefabs == null || animationManager.ExplosionPrefabs.Length == 0)
        {
            animationManager.ExplosionPrefabs = ExplosionPrefabs;
        }
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
            currentDifficulty = string.IsNullOrEmpty(data.difficulty) ? "Normal" : data.difficulty;
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
            // Safer initialization to find the base Food Type
            if (item.name.Contains("_"))
            {
                var parts = item.name.Split('_');
                // Usually Format: BonusType_FoodType (e.g., Striped_Burger) or FoodType_BonusType
                // We check if any part matches a known food type
                
                string foundType = null;
                foreach (var part in parts)
                {
                    var match = FoodPrefabs.FirstOrDefault(x => x.name.Contains(part.Trim()));
                    if (match != null)
                    {
                        foundType = match.name;
                        break;
                    }
                }

                if (foundType != null)
                {
                    item.GetComponent<FoodItem>().Type = foundType;
                }
                else
                {
                    // Fallback to original logic if simple split
                    if (parts.Length > 1)
                    {
                         try {
                            item.GetComponent<FoodItem>().Type = FoodPrefabs.
                                Where(x => x.GetComponent<FoodItem>().Type.Contains(parts[1].Trim())).FirstOrDefault()?.name ?? "";
                         } catch {}
                    }
                }
            }
        }
    }

    public void InitializeBoardAndSpawnPositions()
    {
        InitializeVariables();
        AdjustCameraAndBoardPosition();

        if (grid != null)
        {
            DestroyAllFood();
        }

        grid = new GridSystem();
        SpawnPositions = new Vector2[GameConstants.Columns];

        for (int row = 0; row < GameConstants.Rows; row++)
        {
            for (int column = 0; column < GameConstants.Columns; column++)
            {
                GameObject newFood = GetRandomFood();

                // Prevent initial matches
                int maxIterations = 0;
                while (maxIterations < 100 &&
                    ((column >= 2 && grid[row, column - 1].GetComponent<FoodItem>()
                        .IsSameType(newFood.GetComponent<FoodItem>())
                        && grid[row, column - 2].GetComponent<FoodItem>().IsSameType(newFood.GetComponent<FoodItem>()))
                    ||
                    (row >= 2 && grid[row - 1, column].GetComponent<FoodItem>()
                        .IsSameType(newFood.GetComponent<FoodItem>())
                        && grid[row - 2, column].GetComponent<FoodItem>().IsSameType(newFood.GetComponent<FoodItem>()))))
                {
                    newFood = GetRandomFood();
                    maxIterations++;
                }

                InstantiateAndPlaceNewFood(row, column, newFood);
            }
        }

        SetupSpawnPositions();
        
        // Final pass to ensure no matches exist (Brute Force Fix)
        ValidateBoard();

        // Spawn Pre-Game Booster if selected
        SpawnPreGameBooster();
    }

    private void SpawnPreGameBooster()
    {
        string boostersString = PlayerPrefs.GetString("SelectedBoosters", "");
        
        // Backward compatibility for single selection
        if (string.IsNullOrEmpty(boostersString) && PlayerPrefs.HasKey("SelectedBooster"))
        {
            boostersString = PlayerPrefs.GetString("SelectedBooster", "");
        }
        
        if (string.IsNullOrEmpty(boostersString)) return;

        string[] boosters = boostersString.Split(',');
        
        foreach (var boosterName in boosters)
        {
            if (string.IsNullOrEmpty(boosterName)) continue;
            
            Debug.Log($"Attempting to spawn Pre-Game Booster: {boosterName}");
            SpawnSingleBooster(boosterName);
        }
    }

    private void SpawnSingleBooster(string boosterName)
    {
        GameObject prefabToSpawn = null;
        
        // 1. Try to find exact match in BonusPrefabs
        if (BonusPrefabs != null)
        {
            foreach (var prefab in BonusPrefabs)
            {
                if (prefab.name.IndexOf(boosterName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    prefabToSpawn = prefab;
                    break;
                }
            }
        }
        
        // 2. Fallback Mapping if not found
        if (prefabToSpawn == null && BonusPrefabs != null)
        {
            if (boosterName.Equals("Oven", System.StringComparison.OrdinalIgnoreCase))
            {
                // Oven -> ColorBomb or Explosion
                prefabToSpawn = BonusPrefabs.FirstOrDefault(p => p.name.Contains("ColorBomb") || p.name.Contains("Rainbow") || p.name.Contains("Oven"));
            }
            else if (boosterName.Equals("Hat", System.StringComparison.OrdinalIgnoreCase))
            {
                // Hat -> Magic/Special
                prefabToSpawn = BonusPrefabs.FirstOrDefault(p => p.name.Contains("Hat") || p.name.Contains("Magic"));
            }
            else if (boosterName.Equals("Knife", System.StringComparison.OrdinalIgnoreCase))
            {
                 // Knife -> Striped/Line
                 prefabToSpawn = BonusPrefabs.FirstOrDefault(p => p.name.Contains("Striped") || p.name.Contains("Line") || p.name.Contains("Knife"));
            }
            else if (boosterName.Equals("Pan", System.StringComparison.OrdinalIgnoreCase))
            {
                // Pan -> Explosion/Wrapped
                prefabToSpawn = BonusPrefabs.FirstOrDefault(p => p.name.Contains("Explosion") || p.name.Contains("Wrapped") || p.name.Contains("Pan"));
            }
        }

        if (prefabToSpawn != null)
        {
            // Spawn at random location
            int randRow = Random.Range(0, GameConstants.Rows);
            int randCol = Random.Range(0, GameConstants.Columns);
            
            GameObject oldItem = grid[randRow, randCol];
            if (oldItem != null)
            {
                // Remove from grid immediately to avoid race conditions
                grid.Remove(oldItem); 
                Destroy(oldItem);
            }
            
            GameObject newBonus = Instantiate(prefabToSpawn, BottomRight + new Vector2(randCol * CandySize.x, randRow * CandySize.y), Quaternion.identity);
            
            // IMMEDIATE GRID UPDATE to prevent MissingReferenceException in other scripts
            grid[randRow, randCol] = newBonus;
            
            // Fix FoodItem component
            var foodItem = newBonus.GetComponent<FoodItem>();
            if (foodItem == null) foodItem = newBonus.AddComponent<FoodItem>();
            
            // Assign properties with SAFETY checks
            string type = foodItem.Type; 
            
            // Try to get type from prefab component if current is empty
            if (string.IsNullOrEmpty(type))
            {
                var prefabFood = prefabToSpawn.GetComponent<FoodItem>();
                if (prefabFood != null) type = prefabFood.Type;
            }
            
            // Fallback: Use prefab name, ensuring it's NEVER null
            if (string.IsNullOrEmpty(type))
            {
                type = prefabToSpawn.name ?? "Bonus_Unknown";
            }
            
            // Final safety net for Assign
            try 
            {
                foodItem.Assign(type, randRow, randCol);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error assigning FoodItem for {boosterName}: {ex.Message}. Type was: '{type}'");
                // Attempt recovery assignment with guaranteed non-null string
                foodItem.Assign("Recovery_" + boosterName, randRow, randCol);
            }
            
            // Ensure BonusType is set
            if (foodItem.Bonus == BonusType.None)
            {
                 var prefabFood = prefabToSpawn.GetComponent<FoodItem>();
                 if (prefabFood != null) foodItem.Bonus = prefabFood.Bonus;
                 
                 // Fallback inference
                 if (foodItem.Bonus == BonusType.None)
                 {
                     if (prefabToSpawn.name.Contains("ColorBomb") || prefabToSpawn.name.Contains("Oven")) 
                        foodItem.Bonus = BonusType.ColorBomb;
                     else if (prefabToSpawn.name.Contains("Explosion") || prefabToSpawn.name.Contains("Pan")) 
                        foodItem.Bonus = BonusType.Explosion;
                     else if (prefabToSpawn.name.Contains("Striped") || prefabToSpawn.name.Contains("Line") || prefabToSpawn.name.Contains("Knife"))
                     {
                         // Check name for direction
                         if (prefabToSpawn.name.IndexOf("Vertical", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                             prefabToSpawn.name.IndexOf("Column", System.StringComparison.OrdinalIgnoreCase) >= 0)
                         {
                             foodItem.Bonus = BonusType.DestroyWholeColumn;
                         }
                         else if (prefabToSpawn.name.IndexOf("Horizontal", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                                  prefabToSpawn.name.IndexOf("Row", System.StringComparison.OrdinalIgnoreCase) >= 0)
                         {
                             foodItem.Bonus = BonusType.DestroyWholeRow;
                         }
                         else
                         {
                             // Randomly assign if not specified
                             foodItem.Bonus = (Random.value > 0.5f) ? BonusType.DestroyWholeRow : BonusType.DestroyWholeColumn;
                         }
                     }
                 }
            }

            Debug.Log($"Spawned {boosterName} at [{randRow},{randCol}] with Type: {type}");
        }
        else
        {
            Debug.LogWarning($"Could not find prefab for booster: {boosterName}");
        }
    }

    private void ValidateBoard()
    {
        // Simple brute force: iterate through board, if match found, replace with different type
        bool hasMatch = true;
        int safety = 0;
        while (hasMatch && safety < 100)
        {
            hasMatch = false;
            for (int row = 0; row < GameConstants.Rows; row++)
            {
                for (int col = 0; col < GameConstants.Columns; col++)
                {
                    var current = grid[row, col];
                    if (current == null) continue;
                    var currentFood = current.GetComponent<FoodItem>();

                    // Check Horizontal
                    if (col >= 2)
                    {
                        var p1 = grid[row, col - 1].GetComponent<FoodItem>();
                        var p2 = grid[row, col - 2].GetComponent<FoodItem>();
                        if (currentFood.IsSameType(p1) && currentFood.IsSameType(p2))
                        {
                            ReplaceWithRandomDifferent(current, row, col);
                            hasMatch = true;
                        }
                    }
                    
                    // Check Vertical
                    if (row >= 2)
                    {
                        var p1 = grid[row - 1, col].GetComponent<FoodItem>();
                        var p2 = grid[row - 2, col].GetComponent<FoodItem>();
                        if (currentFood.IsSameType(p1) && currentFood.IsSameType(p2))
                        {
                            ReplaceWithRandomDifferent(current, row, col);
                            hasMatch = true;
                        }
                    }
                }
            }
            safety++;
        }
    }

    private void ReplaceWithRandomDifferent(GameObject currentGo, int row, int col)
    {
        if (currentGo == null) return;
        var currentFood = currentGo.GetComponent<FoodItem>();
        string oldType = currentFood.Type;
        GameObject newPrefab = GetRandomFood();
        int iterations = 0;
        while (newPrefab.GetComponent<FoodItem>().Type == oldType && iterations < 50)
        {
            newPrefab = GetRandomFood();
            iterations++;
        }
        
        // Remove old
        grid.Remove(currentGo);
        Destroy(currentGo);

        // Add new
        InstantiateAndPlaceNewFood(row, col, newPrefab);
    }

    private void HandleLevelComplete()
    {
        isGameOver = true;
        state = GameState.Win;
        StartCoroutine(ShowWinRoutine());
    }

    private IEnumerator ShowWinRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Wait before showing win UI

        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        
        // Coin Logic
        string attemptKey = "Level_" + currentLevel + "_Attempts";
        bool firstTry = PlayerPrefs.GetInt(attemptKey, 1) == 1; 
        
        int winStreak = PlayerPrefs.GetInt("WinStreak", 0);
        winStreak++; 
        PlayerPrefs.SetInt("WinStreak", winStreak);

        int totalCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        
        int coinsEarned = CoinCalculator.CalculateTotalCoins(
            currentLevel, 
            currentMoves, 
            currentDifficulty, 
            boostersUsed, 
            firstTry, 
            winStreak
        );

        if (uiManager != null) uiManager.ShowWin(coinsEarned);

        PlayerPrefs.SetInt("TotalCoins", totalCoins + coinsEarned);
        Debug.Log($"Level Won! Coins Earned: {coinsEarned}. Total: {totalCoins + coinsEarned}. Streak: {winStreak}. FirstTry: {firstTry}. Difficulty: {currentDifficulty}");

        // Advance level
        PlayerPrefs.SetInt("CurrentLevel", currentLevel + 1);
        PlayerPrefs.Save();
    }

    private void HandleLevelLose()
    {
        if (isGameOver) return;
        isGameOver = true;
        state = GameState.Lose;
        StartCoroutine(ShowLoseRoutine());
    }

    private IEnumerator ShowLoseRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Wait before showing lose UI
        if (uiManager != null) uiManager.ShowLose();
    }

    private void InitializeVariables()
    {
        score = 0;
        currentMoves = maxMoves;
        isGameOver = false;

        // Track Attempts
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        string attemptKey = "Level_" + currentLevel + "_Attempts";
        int attempts = PlayerPrefs.GetInt(attemptKey, 0);
        PlayerPrefs.SetInt(attemptKey, attempts + 1);
        
        // Track Boosters (from Home Screen selection)
        string selected = PlayerPrefs.GetString("SelectedBoosters", "");
        boostersUsed = !string.IsNullOrEmpty(selected);

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

        // We DO NOT change Camera Size here anymore, to preserve UI layout
        // Just center the camera horizontally if needed
        if (Camera.main != null)
        {
             // Optional: Only center X position, keep Y and Size fixed as set in Scene
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

    [Header("Input Settings")]
    public bool EnableUIBlocking = false; // Set to false to debug swipe issues

    void Update()
    {
        // Debug Instant Win
        if (DebugWin)
        {
            DebugWin = false;
            if (!isGameOver)
            {
                HandleLevelComplete();
            }
            return;
        }

        // Prevent interaction if clicking on UI (Optional)
        if (EnableUIBlocking && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

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
                    // Debug.Log("Selection Started on " + hitGo.name);
                }
                else
                {
                    // Debug.Log("Click hit nothing or no FoodItem. Check Colliders!");
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
                 if (state != GameState.Animating && hitGo != null)
                 {
                     ActivateTapBonus(hitGo);
                 }
                 if (state != GameState.Animating)
                 {
                     state = GameState.None;
                 }
            }
        }
    }

    private void ActivateTapBonus(GameObject item)
    {
        var food = item.GetComponent<FoodItem>();
        if (food == null || food.Bonus == BonusType.None) return;

        // Special handling for Color Bomb (Oven) on tap
        if (BonusTypeUtilities.ContainsColorBomb(food.Bonus))
        {
            // Find a valid adjacent neighbor to use as target color
            GameObject neighbor = GetRandomNeighbor(item);
            if (neighbor != null)
            {
                state = GameState.Animating;
                StopCheckForPotentialMatches();
                StartCoroutine(ActivateBonusRoutine(item, neighbor));
            }
            return;
        }

        state = GameState.Animating;
        StopCheckForPotentialMatches();
        StartCoroutine(ActivateBonusRoutine(item));
    }

    private GameObject GetRandomNeighbor(GameObject item)
    {
        int row = item.GetComponent<FoodItem>().Row;
        int col = item.GetComponent<FoodItem>().Column;
        
        List<GameObject> neighbors = new List<GameObject>();
        
        // Up
        if (row < GameConstants.Rows - 1 && grid[row + 1, col] != null) neighbors.Add(grid[row + 1, col]);
        // Down
        if (row > 0 && grid[row - 1, col] != null) neighbors.Add(grid[row - 1, col]);
        // Right
        if (col < GameConstants.Columns - 1 && grid[row, col + 1] != null) neighbors.Add(grid[row, col + 1]);
        // Left
        if (col > 0 && grid[row, col - 1] != null) neighbors.Add(grid[row, col - 1]);
        
        // Filter out other bonuses or special items if needed? 
        // User said "any adjcet food item".
        // Prefer simple food items if possible.
        var simpleFood = neighbors.Where(n => n.GetComponent<FoodItem>().Bonus == BonusType.None).ToList();
        
        if (simpleFood.Count > 0)
            return simpleFood[UnityEngine.Random.Range(0, simpleFood.Count)];
            
        if (neighbors.Count > 0)
            return neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
            
        return null;
    }

    private IEnumerator ActivateBonusRoutine(GameObject item, GameObject targetForColorBomb = null)
    {
        // Special handling for Flies
        if (BonusTypeUtilities.ContainsFlies(item.GetComponent<FoodItem>().Bonus))
        {
            StartCoroutine(ActivateFliesRoutine(item));
            yield break;
        }

        var bonusMatches = grid.GetBonusArea(item, targetForColorBomb).ToList();
        bonusMatches.Add(item);
        var totalMatches = bonusMatches.Distinct().ToList();

        IncreaseScore(totalMatches.Count * GameConstants.Match3Score);
        if (soundManager != null) soundManager.PlayCrincle();

        var columns = totalMatches.Select(go => go.GetComponent<FoodItem>().Column).Distinct().ToList();

        foreach (var match in totalMatches)
        {
            if (levelGoals != null)
            {
                var shape = match.GetComponent<FoodItem>();
                foreach (var goal in levelGoals)
                {
                    if (shape.Type.Contains(goal.TargetPrefabName))
                    {
                        goal.AmountCollected++;
                        if (uiManager != null) uiManager.UpdateGoalUI(goal);
                    }
                }
            }
        }

        float waveDuration = ApplyWaveDestruction(totalMatches, item);
        if (waveDuration > 0f) yield return new WaitForSeconds(waveDuration + 0.3f);
        
        // Check for Win
        if (CheckWinCondition())
        {
            HandleLevelComplete();
            yield break;
        }

        var collapsedCandyInfo = grid.Collapse(columns);
        var newCandyInfo = CreateNewCandyInSpecificColumns(columns);
        int maxDistance = Mathf.Max(collapsedCandyInfo.MaxDistance, newCandyInfo.MaxDistance);

        MoveAndAnimate(newCandyInfo.AlteredFood, maxDistance);
        MoveAndAnimate(collapsedCandyInfo.AlteredFood, maxDistance);

        yield return new WaitForSeconds(GameConstants.MoveAnimationMinDuration * maxDistance);
        
        // Chain reactions
        var chainedMatches = grid.GetMatches(collapsedCandyInfo.AlteredFood)
                         .Union(grid.GetMatches(newCandyInfo.AlteredFood)).Distinct().ToList();
        
        if (chainedMatches.Count > 0)
        {
            StartCoroutine(FindMatchesAndCollapse(chainedMatches[0], chainedMatches[0])); // Hack to trigger chain? 
            // Actually FindMatchesAndCollapse expects two swapped items.
            // We should reuse the loop logic or recursively call something. 
            // But FindMatchesAndCollapse is designed for SWAP.
            // Better to fall back to StartCheckForPotentialMatches which handles idle state?
            // Wait, existing code has `StartCheckForPotentialMatches` which just highlights hints.
            // It DOES NOT automatically destroy matches.
            
            // The existing `FindMatchesAndCollapse` loop handles chains:
            // `while (totalMatches.Count >= GameConstants.MinimumMatches)`
            
            // So I should probably structure `ActivateBonusRoutine` to enter a similar loop or call a method that handles the "After Collapse" logic.
            // Since I can't easily reuse `FindMatchesAndCollapse` (it requires hitGo, hitGo2 and does undo swap logic),
            // I'll just manually call ProcessMatches(chainedMatches) if I had one.
            
            // For now, let's just loop here like FindMatchesAndCollapse does.
            yield return StartCoroutine(ProcessMatchesChain(chainedMatches));
        }
        else
        {
            state = GameState.None;
            StartCheckForPotentialMatches();
        }
    }

    private IEnumerator ActivateFliesRoutine(GameObject item)
    {
        IncreaseScore(GameConstants.Match3Score);
        if (soundManager != null) soundManager.PlayCrincle();

        GameObject target = GetFlyTarget(new List<GameObject> { item });
        
        List<GameObject> itemsToDestroy = new List<GameObject> { item };
        if (target != null)
        {
            var sr = item.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 1000;
            
            Vector2 targetPos = target.transform.position;
            float duration = 0.5f;
            
            item.transform.DOMove(targetPos, duration).SetEase(Ease.InBack);
            yield return new WaitForSeconds(duration);
            
            itemsToDestroy.Add(target);
        }
        
        float waveDuration = ApplyWaveDestruction(itemsToDestroy);
        if (waveDuration > 0f) yield return new WaitForSeconds(waveDuration + 0.3f);
        
        var columns = itemsToDestroy.Select(go => go.GetComponent<FoodItem>().Column).Distinct().ToList();

        foreach (var m in itemsToDestroy)
        {
            if (levelGoals != null)
            {
                var shape = m.GetComponent<FoodItem>();
                foreach (var goal in levelGoals)
                {
                    if (shape.Type.Contains(goal.TargetPrefabName))
                    {
                        goal.AmountCollected++;
                        if (uiManager != null) uiManager.UpdateGoalUI(goal);
                    }
                }
            }
        }
        
        if (CheckWinCondition())
        {
            HandleLevelComplete();
            yield break;
        }

        var collapsedCandyInfo = grid.Collapse(columns);
        var newCandyInfo = CreateNewCandyInSpecificColumns(columns);
        int maxDistance = Mathf.Max(collapsedCandyInfo.MaxDistance, newCandyInfo.MaxDistance);

        MoveAndAnimate(newCandyInfo.AlteredFood, maxDistance);
        MoveAndAnimate(collapsedCandyInfo.AlteredFood, maxDistance);

        yield return new WaitForSeconds(GameConstants.MoveAnimationMinDuration * maxDistance);
        
        var chainedMatches = grid.GetMatches(collapsedCandyInfo.AlteredFood)
                         .Union(grid.GetMatches(newCandyInfo.AlteredFood)).Distinct().ToList();
        
        if (chainedMatches.Count > 0)
        {
            yield return StartCoroutine(ProcessMatchesChain(chainedMatches));
        }
        else
        {
            state = GameState.None;
            StartCheckForPotentialMatches();
        }
    }

    private GameObject GetFlyTarget(IEnumerable<GameObject> excluded)
    {
        if (levelGoals != null)
        {
            var targetGoals = levelGoals.Where(g => !g.IsComplete()).ToList();
            foreach (var goal in targetGoals)
            {
                for (int r = 0; r < GameConstants.Rows; r++)
                {
                    for (int c = 0; c < GameConstants.Columns; c++)
                    {
                        var go = grid[r, c];
                        if (go != null && !excluded.Contains(go))
                        {
                            if (go.GetComponent<FoodItem>().Type.Contains(goal.TargetPrefabName))
                                return go;
                        }
                    }
                }
            }
        }
        
        List<GameObject> candidates = new List<GameObject>();
        for (int r = 0; r < GameConstants.Rows; r++)
        {
            for (int c = 0; c < GameConstants.Columns; c++)
            {
                var go = grid[r, c];
                if (go != null && !excluded.Contains(go) && go.GetComponent<FoodItem>().Bonus == BonusType.None)
                {
                    candidates.Add(go);
                }
            }
        }
        
        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];
            
        return null;
    }

    private IEnumerator ProcessMatchesChain(List<GameObject> matches)
    {
        int timesRun = 1;
        var totalMatches = matches;
        
        while (totalMatches.Count >= GameConstants.MinimumMatches)
        {
             IncreaseScore((totalMatches.Count - 2) * GameConstants.Match3Score);
             if (timesRun >= 2) IncreaseScore(GameConstants.SubsequentMatchScore);
             if (soundManager != null) soundManager.PlayCrincle();

             var columns = totalMatches.Select(go => go.GetComponent<FoodItem>().Column).Distinct().ToList();

             foreach (var item in totalMatches)
             {
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
             }

             float waveDuration = ApplyWaveDestruction(totalMatches);
             if (waveDuration > 0f) yield return new WaitForSeconds(waveDuration + 0.3f);

            if (CheckWinCondition())
            {
                HandleLevelComplete();
                yield break;
            }

             var collapsedCandyInfo = grid.Collapse(columns);
             var newCandyInfo = CreateNewCandyInSpecificColumns(columns);
             int maxDistance = Mathf.Max(collapsedCandyInfo.MaxDistance, newCandyInfo.MaxDistance);

             MoveAndAnimate(newCandyInfo.AlteredFood, maxDistance);
             MoveAndAnimate(collapsedCandyInfo.AlteredFood, maxDistance);

             yield return new WaitForSeconds(GameConstants.MoveAnimationMinDuration * maxDistance);

             totalMatches = grid.GetMatches(collapsedCandyInfo.AlteredFood)
                 .Union(grid.GetMatches(newCandyInfo.AlteredFood)).Distinct().ToList();
                 
             timesRun++;
        }
        
        state = GameState.None;
        StartCheckForPotentialMatches();
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

        // Check for Bonus Activation on Swap (even if no match-3)
        bool bonusTriggered = false;
        GameObject bonusSource = null;
        
        // 1. Check if hitGo is a Bonus
        var f1 = hitGo.GetComponent<FoodItem>();
        if (f1 != null && f1.Bonus != BonusType.None)
        {
            var bonusMatches = grid.GetBonusArea(hitGo, hitGo2);
            if (bonusMatches.Count() > 0)
            {
                totalMatches.AddRange(bonusMatches);
                totalMatches.Add(hitGo); // Ensure bonus itself is destroyed
                bonusTriggered = true;
                if (bonusSource == null) bonusSource = hitGo;
            }
        }

        // 2. Check if hitGo2 is a Bonus
        var f2 = hitGo2.GetComponent<FoodItem>();
        if (f2 != null && f2.Bonus != BonusType.None)
        {
            var bonusMatches = grid.GetBonusArea(hitGo2, hitGo);
            if (bonusMatches.Count() > 0)
            {
                totalMatches.AddRange(bonusMatches);
                totalMatches.Add(hitGo2); // Ensure bonus itself is destroyed
                bonusTriggered = true;
                if (bonusSource == null) bonusSource = hitGo2;
            }
        }
        
        // Deduplicate
        totalMatches = totalMatches.Distinct().ToList();

        if (totalMatches.Count < GameConstants.MinimumMatches && !bonusTriggered)
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
        else if (b1 == BonusType.Flies || b2 == BonusType.Flies)
        {
            bonusToCreate = BonusType.Flies;
            hitGoCache = (b1 == BonusType.Flies) ? hitGo.GetComponent<FoodItem>() : hitGo2.GetComponent<FoodItem>();
        }
        else if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(b1) || BonusTypeUtilities.ContainsDestroyWholeRowColumn(b2))
        {
            if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(b1))
            {
                 bonusToCreate = b1;
                 hitGoCache = hitGo.GetComponent<FoodItem>();
            }
            else
            {
                 bonusToCreate = b2;
                 hitGoCache = hitGo2.GetComponent<FoodItem>();
            }
        }

        if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(hitGomatchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsDestroyWholeRowColumn(hitGo2matchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsExplosion(hitGomatchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsExplosion(hitGo2matchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsFlies(hitGomatchesInfo.BonusesContained) ||
            BonusTypeUtilities.ContainsFlies(hitGo2matchesInfo.BonusesContained))
        {
            bonusToCreate = BonusType.None;
        }

        bool addBonus = (bonusToCreate != BonusType.None);

        int timesRun = 1;
        while (totalMatches.Count >= GameConstants.MinimumMatches || (timesRun == 1 && bonusTriggered))
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
            }

            // Flies Activation Logic
            List<GameObject> fliesTargets = new List<GameObject>();
            foreach (var item in totalMatches)
            {
                 if (BonusTypeUtilities.ContainsFlies(item.GetComponent<FoodItem>().Bonus))
                 {
                      var target = GetFlyTarget(totalMatches.Union(fliesTargets));
                      if (target != null)
                       {
                            fliesTargets.Add(target);
                            // Visual: Spawn a flying sprite
                            GameObject flyer = new GameObject("FlyEffect");
                            flyer.transform.position = item.transform.position;
                            var sr = flyer.AddComponent<SpriteRenderer>();
                            sr.sprite = item.GetComponent<SpriteRenderer>().sprite;
                            sr.sortingOrder = 1000;
                            
                            flyer.transform.DOMove(target.transform.position, 0.5f).SetEase(Ease.InBack).OnComplete(() => {
                                Destroy(flyer);
                            });
                       }
                 }
            }
            if (fliesTargets.Count > 0)
            {
                totalMatches.AddRange(fliesTargets);
                totalMatches = totalMatches.Distinct().ToList();
                // Recalculate columns for collapse
                columns = totalMatches.Select(go => go.GetComponent<FoodItem>().Column).Distinct().ToList();
            }

            float waveDuration = ApplyWaveDestruction(totalMatches, bonusSource);
            if (waveDuration > 0f) yield return new WaitForSeconds(waveDuration + 0.3f);

            // Check for Win
            if (CheckWinCondition())
            {
                HandleLevelComplete();
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
            HandleLevelLose();
        }
        else if (!isGameOver)
        {
            StartCheckForPotentialMatches();
        }
    }

    private void CreateBonus(FoodItem hitGoCache, BonusType bonusType)
    {
        GameObject bonusPrefab = GetBonusFromType(hitGoCache.Type, bonusType);
        if (bonusPrefab == null) return;

        GameObject Bonus = Instantiate(bonusPrefab, BottomRight
            + new Vector2(hitGoCache.Column * CandySize.x,
                hitGoCache.Row * CandySize.y), Quaternion.identity)
            as GameObject;
        grid[hitGoCache.Row, hitGoCache.Column] = Bonus;
        
        var BonusShape = Bonus.GetComponent<FoodItem>();
        if (BonusShape == null)
        {
            Debug.LogError($"Bonus prefab '{bonusPrefab.name}' is missing the 'FoodItem' script! Auto-adding it, but please fix the prefab.");
            BonusShape = Bonus.AddComponent<FoodItem>();
        }
        
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

    private GameObject GetBonusFromType(string type, BonusType bonusType)
    {
        if (string.IsNullOrEmpty(type)) return null;

        // 1. Color Bomb Check (5 in a straight line)
        if (BonusTypeUtilities.ContainsColorBomb(bonusType))
        {
            foreach (var item in BonusPrefabs)
            {
                if (item.name.IndexOf("ColorBomb", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                    item.name.IndexOf("Rainbow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.name.IndexOf("Oven", System.StringComparison.OrdinalIgnoreCase) >= 0) // Oven is now Color Bomb
                    return item;
            }
        }

        string typeLower = type.ToLower();

        // 2. Specific Bonus Type Check (Type + Bonus Name)
        foreach (var item in BonusPrefabs)
        {
            var foodItem = item.GetComponent<FoodItem>();
            if (foodItem != null && !string.IsNullOrEmpty(foodItem.Type) && type.Contains(foodItem.Type))
            {
                string nameLower = item.name.ToLower();

                if (BonusTypeUtilities.ContainsDestroyWholeRow(bonusType))
                {
                    if (nameLower.Contains("horizontal") || nameLower.Contains("row") || nameLower.Contains("sideways"))
                        return item;
                }
                else if (BonusTypeUtilities.ContainsDestroyWholeColumn(bonusType))
                {
                    if (nameLower.Contains("vertical") || nameLower.Contains("column") || nameLower.Contains("upside"))
                        return item;
                }
                else if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(bonusType))
                {
                    if (nameLower.Contains("striped") || nameLower.Contains("knife") || nameLower.Contains("blender"))
                        return item;
                }
                else if (BonusTypeUtilities.ContainsExplosion(bonusType))
                {
                    if (nameLower.Contains("explosion") || nameLower.Contains("package") || 
                        nameLower.Contains("wrapped") || nameLower.Contains("bomb") || 
                        nameLower.Contains("pan")) // Pan is Explosion
                        return item;
                }
                else if (BonusTypeUtilities.ContainsFlies(bonusType))
                {
                     if (nameLower.Contains("flies") || nameLower.Contains("propeller") || 
                         nameLower.Contains("bird") || nameLower.Contains("rocket") || 
                         nameLower.Contains("plane"))
                        return item;
                }
            }
        }

        // 3. Generic Bonus Type Check (Bonus Name Only - Shared Prefab)
        foreach (var item in BonusPrefabs)
        {
            string nameLower = item.name.ToLower();

            if (BonusTypeUtilities.ContainsDestroyWholeRow(bonusType))
            {
                if (nameLower.Contains("horizontal") || nameLower.Contains("row") || nameLower.Contains("sideways"))
                    return item;
            }
            else if (BonusTypeUtilities.ContainsDestroyWholeColumn(bonusType))
            {
                if (nameLower.Contains("vertical") || nameLower.Contains("column") || nameLower.Contains("upside"))
                    return item;
            }
            else if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(bonusType))
            {
                if (nameLower.Contains("striped") || nameLower.Contains("knife") || nameLower.Contains("blender"))
                    return item;
            }
            else if (BonusTypeUtilities.ContainsExplosion(bonusType))
            {
                if (nameLower.Contains("explosion") || nameLower.Contains("package") || 
                    nameLower.Contains("wrapped") || nameLower.Contains("bomb") || 
                    nameLower.Contains("pan")) // Pan is Explosion
                    return item;
            }
            else if (BonusTypeUtilities.ContainsFlies(bonusType))
            {
                 if (nameLower.Contains("flies") || nameLower.Contains("propeller") || 
                     nameLower.Contains("bird") || nameLower.Contains("rocket") || 
                     nameLower.Contains("plane"))
                    return item;
            }
        }

        // 4. Fallback: Return any matching food type if specific variant not found
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
        var shape = item.GetComponent<FoodItem>();
        List<GameObject> items = new List<GameObject>();
        if (BonusTypeUtilities.ContainsDestroyWholeRow(shape.Bonus))
        {
            int row = shape.Row;
            for (int c = 0; c < GameConstants.Columns; c++)
            {
                if (grid[row, c] != null) items.Add(grid[row, c]);
            }
        }
        else if (BonusTypeUtilities.ContainsDestroyWholeColumn(shape.Bonus))
        {
            int col = shape.Column;
            for (int r = 0; r < GameConstants.Rows; r++)
            {
                if (grid[r, col] != null) items.Add(grid[r, col]);
            }
        }
        else
        {
            items.Add(item);
        }
        StartCoroutine(DestroyItemsRoutine(items, item));
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
        StartCoroutine(DestroyItemsRoutine(items, item));
    }

    public void ApplyPanPowerup(GameObject item)
    {
        int row = item.GetComponent<FoodItem>().Row;
        List<GameObject> items = new List<GameObject>();
        for (int col = 0; col < GameConstants.Columns; col++)
        {
            if (grid[row, col] != null) items.Add(grid[row, col]);
        }
        StartCoroutine(DestroyItemsRoutine(items, item));
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
        StartCoroutine(DestroyItemsRoutine(items, item));
    }

    public void ApplyBlenderPowerup()
    {
        ShuffleBoard();
    }

    private float ApplyWaveDestruction(List<GameObject> items, GameObject centerItem = null)
    {
        if (items == null || items.Count == 0) return 0f;

        // Logical Removal
        foreach (var item in items)
        {
            if (item != null) grid.Remove(item);
        }

        // Visual Animation (Delegated to AnimationManager)
        if (animationManager != null)
        {
            return animationManager.AnimateDestruction(items, centerItem);
        }
        else
        {
            // Fallback if no animation manager (shouldn't happen)
            foreach(var item in items) if(item!=null) Destroy(item);
            return 0f;
        }
    }

    private IEnumerator DestroyItemsRoutine(IEnumerable<GameObject> items, GameObject centerItem = null)
    {
        state = GameState.Animating;

        List<int> affectedColumns = new List<int>();

        var list = new List<GameObject>(items.Where(i => i != null));
        if (list.Count == 0)
        {
            state = GameState.None;
            yield break;
        }

        // Calculate affected columns for collapse before destruction
        foreach (var item in list)
        {
            var shape = item.GetComponent<FoodItem>();
            if (!affectedColumns.Contains(shape.Column))
                affectedColumns.Add(shape.Column);
        }

        float maxDelay = ApplyWaveDestruction(list, centerItem);

        float animTail = 0.5f;
        yield return new WaitForSeconds(maxDelay + animTail);

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
