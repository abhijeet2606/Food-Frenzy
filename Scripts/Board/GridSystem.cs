using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridSystem
{
    private GameObject[,] grid = new GameObject[GameConstants.Rows, GameConstants.Columns];
    private GameObject backupG1;
    private GameObject backupG2;

    public GameObject this[int row, int column]
    {
        get
        {
            try
            {
                return grid[row, column];
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        set
        {
            grid[row, column] = value;
        }
    }

    public BonusType AnalyzeMatchShape(GameObject go)
    {
        var h = GetMatchesHorizontally(go);
        var v = GetMatchesVertically(go);
        var s = GetMatchesSquare(go);
        
        int hCount = h.Count();
        int vCount = v.Count();
        int sCount = s.Count();
        
        if (hCount >= 5 || vCount >= 5) return BonusType.ColorBomb;
        if (hCount >= 3 && vCount >= 3) return BonusType.Explosion; // T or L shape
        
        // Horizontal Match -> Vertical Knife (Destroy Column)
        if (hCount == 4) return BonusType.DestroyWholeColumn;
        
        // Vertical Match -> Horizontal Knife (Destroy Row)
        if (vCount == 4) return BonusType.DestroyWholeRow;
        
        // Square Match -> Flies
        if (sCount >= 4) return BonusType.Flies;
        
        return BonusType.None;
    }

    public void Swap(GameObject g1, GameObject g2)
    {
        backupG1 = g1;
        backupG2 = g2;

        var g1Shape = g1.GetComponent<FoodItem>();
        var g2Shape = g2.GetComponent<FoodItem>();

        int g1Row = g1Shape.Row;
        int g1Column = g1Shape.Column;
        int g2Row = g2Shape.Row;
        int g2Column = g2Shape.Column;

        var temp = grid[g1Row, g1Column];
        grid[g1Row, g1Column] = grid[g2Row, g2Column];
        grid[g2Row, g2Column] = temp;

        FoodItem.SwapColumnRow(g1Shape, g2Shape);
    }

    public void UndoSwap()
    {
        if (backupG1 == null || backupG2 == null)
            throw new Exception("Backup is null");

        Swap(backupG1, backupG2);
    }

    public IEnumerable<GameObject> GetBonusArea(GameObject go, GameObject other = null)
    {
        List<GameObject> matches = new List<GameObject>();
        var food = go.GetComponent<FoodItem>();
        if (food == null) return matches;

        if (BonusTypeUtilities.ContainsExplosion(food.Bonus))
        {
             int row = food.Row;
             int column = food.Column;
             for (int r = row - 1; r <= row + 1; r++)
             {
                 for (int c = column - 1; c <= column + 1; c++)
                 {
                     if (r >= 0 && r < GameConstants.Rows && c >= 0 && c < GameConstants.Columns)
                     {
                         if (grid[r, c] != null) matches.Add(grid[r, c]);
                     }
                 }
             }
        }
        else if (BonusTypeUtilities.ContainsDestroyWholeRow(food.Bonus))
        {
            // Row
            for (int c = 0; c < GameConstants.Columns; c++)
            {
                if (grid[food.Row, c] != null) matches.Add(grid[food.Row, c]);
            }
        }
        else if (BonusTypeUtilities.ContainsDestroyWholeColumn(food.Bonus))
        {
            // Column
            for (int r = 0; r < GameConstants.Rows; r++)
            {
                if (grid[r, food.Column] != null) matches.Add(grid[r, food.Column]);
            }
        }
        else if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(food.Bonus))
        {
            // Row
            for (int c = 0; c < GameConstants.Columns; c++)
            {
                if (grid[food.Row, c] != null) matches.Add(grid[food.Row, c]);
            }
            // Column
            for (int r = 0; r < GameConstants.Rows; r++)
            {
                if (grid[r, food.Column] != null) matches.Add(grid[r, food.Column]);
            }
        }
        else if (BonusTypeUtilities.ContainsColorBomb(food.Bonus))
        {
            // Color Bomb Logic: Destroy all items of the type of 'other'
            if (other != null)
            {
                var otherFood = other.GetComponent<FoodItem>();
                if (otherFood != null && !string.IsNullOrEmpty(otherFood.Type))
                {
                    string targetType = otherFood.Type;
                    for (int r = 0; r < GameConstants.Rows; r++)
                    {
                        for (int c = 0; c < GameConstants.Columns; c++)
                        {
                            var item = grid[r, c];
                            if (item != null)
                            {
                                var itemFood = item.GetComponent<FoodItem>();
                                if (itemFood != null && itemFood.Type == targetType)
                                {
                                    matches.Add(item);
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (BonusTypeUtilities.ContainsFlies(food.Bonus))
        {
             if (other != null)
             {
                 matches.Add(other);
             }
        }

        return matches;
    }

    public IEnumerable<GameObject> GetMatches(IEnumerable<GameObject> gos)
    {
        List<GameObject> matches = new List<GameObject>();
        foreach (var go in gos)
        {
            matches.AddRange(GetMatches(go).MatchedFood);
        }
        return matches.Distinct();
    }

    public MatchesInfo GetMatches(GameObject go)
    {
        MatchesInfo matchesInfo = new MatchesInfo();

        var horizontalMatches = GetMatchesHorizontally(go);
        if (ContainsDestroyRowColumnBonus(horizontalMatches))
        {
            horizontalMatches = GetEntireRow(go);
            if (!BonusTypeUtilities.ContainsDestroyWholeRowColumn(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.DestroyWholeRowColumn;
        }
        if (ContainsExplosionBonus(horizontalMatches))
        {
            var explosionMatches = GetExplosionRange(horizontalMatches);
            matchesInfo.AddObjectRange(explosionMatches);
            if (!BonusTypeUtilities.ContainsExplosion(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.Explosion;
        }
        if (ContainsColorBomb(horizontalMatches))
        {
            var colorBombMatches = GetColorBombRange(horizontalMatches);
            matchesInfo.AddObjectRange(colorBombMatches);
            if (!BonusTypeUtilities.ContainsColorBomb(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.ColorBomb;
        }
        if (ContainsFlies(horizontalMatches))
        {
             if (!BonusTypeUtilities.ContainsFlies(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.Flies;
        }
        matchesInfo.AddObjectRange(horizontalMatches);

        var verticalMatches = GetMatchesVertically(go);
        if (ContainsDestroyRowColumnBonus(verticalMatches))
        {
            verticalMatches = GetEntireColumn(go);
            if (!BonusTypeUtilities.ContainsDestroyWholeRowColumn(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.DestroyWholeRowColumn;
        }
        if (ContainsExplosionBonus(verticalMatches))
        {
            var explosionMatches = GetExplosionRange(verticalMatches);
            matchesInfo.AddObjectRange(explosionMatches);
            if (!BonusTypeUtilities.ContainsExplosion(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.Explosion;
        }
        if (ContainsColorBomb(verticalMatches))
        {
            var colorBombMatches = GetColorBombRange(verticalMatches);
            matchesInfo.AddObjectRange(colorBombMatches);
            if (!BonusTypeUtilities.ContainsColorBomb(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.ColorBomb;
        }
        if (ContainsFlies(verticalMatches))
        {
             if (!BonusTypeUtilities.ContainsFlies(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.Flies;
        }
        matchesInfo.AddObjectRange(verticalMatches);

        var squareMatches = GetMatchesSquare(go);
        if (ContainsDestroyRowColumnBonus(squareMatches))
        {
            if (!BonusTypeUtilities.ContainsDestroyWholeRowColumn(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.DestroyWholeRowColumn;
        }
        if (ContainsExplosionBonus(squareMatches))
        {
            var explosionMatches = GetExplosionRange(squareMatches);
            matchesInfo.AddObjectRange(explosionMatches);
            if (!BonusTypeUtilities.ContainsExplosion(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.Explosion;
        }
        if (ContainsColorBomb(squareMatches))
        {
            var colorBombMatches = GetColorBombRange(squareMatches);
            matchesInfo.AddObjectRange(colorBombMatches);
            if (!BonusTypeUtilities.ContainsColorBomb(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.ColorBomb;
        }
        if (ContainsFlies(squareMatches))
        {
            if (!BonusTypeUtilities.ContainsFlies(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.Flies;
        }
        matchesInfo.AddObjectRange(squareMatches);

        return matchesInfo;
    }

    private bool ContainsFlies(IEnumerable<GameObject> matches)
    {
        if (matches.Count() >= GameConstants.MinimumMatches)
        {
            foreach (var go in matches)
            {
                if (BonusTypeUtilities.ContainsFlies(go.GetComponent<FoodItem>().Bonus))
                    return true;
            }
        }
        return false;
    }

    private bool ContainsColorBomb(IEnumerable<GameObject> matches)
    {
        if (matches.Count() >= GameConstants.MinimumMatches)
        {
            foreach (var go in matches)
            {
                if (BonusTypeUtilities.ContainsColorBomb(go.GetComponent<FoodItem>().Bonus))
                    return true;
            }
        }
        return false;
    }

    private IEnumerable<GameObject> GetColorBombRange(IEnumerable<GameObject> matches)
    {
        List<GameObject> allMatches = new List<GameObject>();
        foreach (var go in matches)
        {
            if (BonusTypeUtilities.ContainsColorBomb(go.GetComponent<FoodItem>().Bonus))
            {
                string type = go.GetComponent<FoodItem>().Type;
                for (int r = 0; r < GameConstants.Rows; r++)
                {
                    for (int c = 0; c < GameConstants.Columns; c++)
                    {
                        if (grid[r, c] != null && grid[r, c].GetComponent<FoodItem>().Type == type)
                        {
                            allMatches.Add(grid[r, c]);
                        }
                    }
                }
            }
        }
        return allMatches;
    }

    private bool ContainsExplosionBonus(IEnumerable<GameObject> matches)
    {
        if (matches.Count() >= GameConstants.MinimumMatches)
        {
            foreach (var go in matches)
            {
                if (BonusTypeUtilities.ContainsExplosion(go.GetComponent<FoodItem>().Bonus))
                    return true;
            }
        }
        return false;
    }

    private IEnumerable<GameObject> GetExplosionRange(IEnumerable<GameObject> matches)
    {
        List<GameObject> range = new List<GameObject>();
        foreach (var go in matches)
        {
            if (BonusTypeUtilities.ContainsExplosion(go.GetComponent<FoodItem>().Bonus))
            {
                int row = go.GetComponent<FoodItem>().Row;
                int column = go.GetComponent<FoodItem>().Column;
                for (int r = row - 1; r <= row + 1; r++)
                {
                    for (int c = column - 1; c <= column + 1; c++)
                    {
                        if (r >= 0 && r < GameConstants.Rows && c >= 0 && c < GameConstants.Columns)
                        {
                            if (grid[r, c] != null) range.Add(grid[r, c]);
                        }
                    }
                }
            }
        }
        return range;
    }

    private bool ContainsDestroyRowColumnBonus(IEnumerable<GameObject> matches)
    {
        if (matches.Count() >= GameConstants.MinimumMatches)
        {
            foreach (var go in matches)
            {
                if (BonusTypeUtilities.ContainsDestroyWholeRowColumn(go.GetComponent<FoodItem>().Bonus))
                    return true;
            }
        }
        return false;
    }

    private IEnumerable<GameObject> GetEntireRow(GameObject go)
    {
        List<GameObject> matches = new List<GameObject>();
        int row = go.GetComponent<FoodItem>().Row;
        for (int column = 0; column < GameConstants.Columns; column++)
        {
            matches.Add(grid[row, column]);
        }
        return matches;
    }

    private IEnumerable<GameObject> GetEntireColumn(GameObject go)
    {
        List<GameObject> matches = new List<GameObject>();
        int column = go.GetComponent<FoodItem>().Column;
        for (int row = 0; row < GameConstants.Rows; row++)
        {
            matches.Add(grid[row, column]);
        }
        return matches;
    }

    private IEnumerable<GameObject> GetMatchesSquare(GameObject go)
    {
        List<GameObject> matches = new List<GameObject>();
        var shape = go.GetComponent<FoodItem>();
        int r = shape.Row;
        int c = shape.Column;

        // Check 4 quadrants
        // Top-Right
        if (CheckSquare(r, c, r + 1, c, r, c + 1, r + 1, c + 1, shape))
            AddSquare(matches, r, c, r + 1, c, r, c + 1, r + 1, c + 1);

        // Top-Left
        if (CheckSquare(r, c, r + 1, c, r, c - 1, r + 1, c - 1, shape))
            AddSquare(matches, r, c, r + 1, c, r, c - 1, r + 1, c - 1);

        // Bottom-Right
        if (CheckSquare(r, c, r - 1, c, r, c + 1, r - 1, c + 1, shape))
            AddSquare(matches, r, c, r - 1, c, r, c + 1, r - 1, c + 1);

        // Bottom-Left
        if (CheckSquare(r, c, r - 1, c, r, c - 1, r - 1, c - 1, shape))
            AddSquare(matches, r, c, r - 1, c, r, c - 1, r - 1, c - 1);

        return matches.Distinct();
    }

    private bool CheckSquare(int r1, int c1, int r2, int c2, int r3, int c3, int r4, int c4, FoodItem shape)
    {
        if (!IsValidAndSame(r2, c2, shape)) return false;
        if (!IsValidAndSame(r3, c3, shape)) return false;
        if (!IsValidAndSame(r4, c4, shape)) return false;
        return true;
    }

    private bool IsValidAndSame(int r, int c, FoodItem shape)
    {
        if (r < 0 || r >= GameConstants.Rows || c < 0 || c >= GameConstants.Columns) return false;
        if (grid[r, c] == null) return false;
        return grid[r, c].GetComponent<FoodItem>().IsSameType(shape);
    }

    private void AddSquare(List<GameObject> matches, int r1, int c1, int r2, int c2, int r3, int c3, int r4, int c4)
    {
        matches.Add(grid[r1, c1]);
        matches.Add(grid[r2, c2]);
        matches.Add(grid[r3, c3]);
        matches.Add(grid[r4, c4]);
    }

    private IEnumerable<GameObject> GetMatchesHorizontally(GameObject go)
    {
        List<GameObject> matches = new List<GameObject>();
        matches.Add(go);
        var shape = go.GetComponent<FoodItem>();
        
        //check left
        if (shape.Column != 0)
            for (int column = shape.Column - 1; column >= 0; column--)
            {
                if (grid[shape.Row, column].GetComponent<FoodItem>().IsSameType(shape))
                    matches.Add(grid[shape.Row, column]);
                else
                    break;
            }

        //check right
        if (shape.Column != GameConstants.Columns - 1)
            for (int column = shape.Column + 1; column < GameConstants.Columns; column++)
            {
                if (grid[shape.Row, column].GetComponent<FoodItem>().IsSameType(shape))
                    matches.Add(grid[shape.Row, column]);
                else
                    break;
            }

        if (matches.Count < GameConstants.MinimumMatches)
            matches.Clear();

        return matches.Distinct();
    }

    private IEnumerable<GameObject> GetMatchesVertically(GameObject go)
    {
        List<GameObject> matches = new List<GameObject>();
        matches.Add(go);
        var shape = go.GetComponent<FoodItem>();
        
        //check bottom
        if (shape.Row != 0)
            for (int row = shape.Row - 1; row >= 0; row--)
            {
                if (grid[row, shape.Column] != null &&
                    grid[row, shape.Column].GetComponent<FoodItem>().IsSameType(shape))
                {
                    matches.Add(grid[row, shape.Column]);
                }
                else
                    break;
            }

        //check top
        if (shape.Row != GameConstants.Rows - 1)
            for (int row = shape.Row + 1; row < GameConstants.Rows; row++)
            {
                if (grid[row, shape.Column] != null && 
                    grid[row, shape.Column].GetComponent<FoodItem>().IsSameType(shape))
                {
                    matches.Add(grid[row, shape.Column]);
                }
                else
                    break;
            }

        if (matches.Count < GameConstants.MinimumMatches)
            matches.Clear();

        return matches.Distinct();
    }

    public void Remove(GameObject item)
    {
        grid[item.GetComponent<FoodItem>().Row, item.GetComponent<FoodItem>().Column] = null;
    }

    public AlteredFoodInfo Collapse(IEnumerable<int> columns)
    {
        AlteredFoodInfo collapseInfo = new AlteredFoodInfo();

        foreach (var column in columns)
        {
            for (int row = 0; row < GameConstants.Rows - 1; row++)
            {
                if (grid[row, column] == null)
                {
                    for (int row2 = row + 1; row2 < GameConstants.Rows; row2++)
                    {
                        if (grid[row2, column] != null)
                        {
                            grid[row, column] = grid[row2, column];
                            grid[row2, column] = null;

                            if (row2 - row > collapseInfo.MaxDistance) 
                                collapseInfo.MaxDistance = row2 - row;

                            grid[row, column].GetComponent<FoodItem>().Row = row;
                            grid[row, column].GetComponent<FoodItem>().Column = column;

                            collapseInfo.AddFood(grid[row, column]);
                            break;
                        }
                    }
                }
            }
        }
        return collapseInfo;
    }

    public IEnumerable<GridPosition> GetEmptyItemsOnColumn(int column)
    {
        List<GridPosition> emptyItems = new List<GridPosition>();
        for (int row = 0; row < GameConstants.Rows; row++)
        {
            if (grid[row, column] == null)
                emptyItems.Add(new GridPosition { Row = row, Column = column });
        }
        return emptyItems;
    }
}
