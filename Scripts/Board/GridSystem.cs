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
        matchesInfo.AddObjectRange(horizontalMatches);

        var verticalMatches = GetMatchesVertically(go);
        if (ContainsDestroyRowColumnBonus(verticalMatches))
        {
            verticalMatches = GetEntireColumn(go);
            if (!BonusTypeUtilities.ContainsDestroyWholeRowColumn(matchesInfo.BonusesContained))
                matchesInfo.BonusesContained |= BonusType.DestroyWholeRowColumn;
        }
        matchesInfo.AddObjectRange(verticalMatches);

        return matchesInfo;
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
