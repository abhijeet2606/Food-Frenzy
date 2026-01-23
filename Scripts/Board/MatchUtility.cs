using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MatchUtility
{
    public static IEnumerator AnimatePotentialMatches(IEnumerable<GameObject> potentialMatches)
    {
        for (float i = 1f; i >= 0.3f; i -= 0.1f)
        {
            foreach (var item in potentialMatches)
            {
                if (item == null) continue;
                Color c = item.GetComponent<SpriteRenderer>().color;
                c.a = i;
                item.GetComponent<SpriteRenderer>().color = c;
            }
            yield return new WaitForSeconds(GameConstants.OpacityAnimationFrameDelay);
        }
        for (float i = 0.3f; i <= 1f; i += 0.1f)
        {
            foreach (var item in potentialMatches)
            {
                if (item == null) continue;
                Color c = item.GetComponent<SpriteRenderer>().color;
                c.a = i;
                item.GetComponent<SpriteRenderer>().color = c;
            }
            yield return new WaitForSeconds(GameConstants.OpacityAnimationFrameDelay);
        }
    }

    public static bool AreVerticalOrHorizontalNeighbors(FoodItem s1, FoodItem s2)
    {
        return (s1.Column == s2.Column || s1.Row == s2.Row)
            && Mathf.Abs(s1.Column - s2.Column) <= 1
            && Mathf.Abs(s1.Row - s2.Row) <= 1;
    }

    public static IEnumerable<GameObject> GetPotentialMatches(GridSystem grid)
    {
        List<List<GameObject>> matches = new List<List<GameObject>>();
       
        for (int row = 0; row < GameConstants.Rows; row++)
        {
            for (int column = 0; column < GameConstants.Columns; column++)
            {
                var matches1 = CheckHorizontal1(row, column, grid);
                var matches2 = CheckHorizontal2(row, column, grid);
                var matches3 = CheckHorizontal3(row, column, grid);
                var matches4 = CheckVertical1(row, column, grid);
                var matches5 = CheckVertical2(row, column, grid);
                var matches6 = CheckVertical3(row, column, grid);

                if (matches1 != null) matches.Add(matches1);
                if (matches2 != null) matches.Add(matches2);
                if (matches3 != null) matches.Add(matches3);
                if (matches4 != null) matches.Add(matches4);
                if (matches5 != null) matches.Add(matches5);
                if (matches6 != null) matches.Add(matches6);

                if (matches.Count >= 3)
                    return matches[Random.Range(0, matches.Count - 1)];

                if(row >= GameConstants.Rows / 2 && matches.Count > 0 && matches.Count <=2)
                    return matches[Random.Range(0, matches.Count - 1)];
            }
        }
        return null;
    }

    public static List<GameObject> CheckHorizontal1(int row, int column, GridSystem grid)
    {
        if (column <= GameConstants.Columns - 2)
        {
            if (grid[row, column].GetComponent<FoodItem>().
                IsSameType(grid[row, column + 1].GetComponent<FoodItem>()))
            {
                if (row >= 1 && column >= 1 && grid[row - 1, column - 1].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row, column + 1],
                        grid[row - 1, column - 1]
                    };
                if (row <= GameConstants.Rows - 2 && column >= 1 && grid[row + 1, column - 1].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row, column + 1],
                        grid[row + 1, column - 1]
                    };
            }
        }
        return null;
    }

    public static List<GameObject> CheckHorizontal2(int row, int column, GridSystem grid)
    {
        if (column <= GameConstants.Columns - 3)
        {
            if (grid[row, column].GetComponent<FoodItem>().
                IsSameType(grid[row, column + 1].GetComponent<FoodItem>()))
            {
                if (grid[row, column + 2].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row, column + 1],
                        grid[row, column + 2]
                    };
            }
        }
        return null;
    }

    public static List<GameObject> CheckHorizontal3(int row, int column, GridSystem grid)
    {
        if (column <= GameConstants.Columns - 2)
        {
            if (grid[row, column].GetComponent<FoodItem>().
                IsSameType(grid[row, column + 1].GetComponent<FoodItem>()))
            {
                if (row >= 1 && column <= GameConstants.Columns - 3 && grid[row - 1, column + 2].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row, column + 1],
                        grid[row - 1, column + 2]
                    };
                if (row <= GameConstants.Rows - 2 && column <= GameConstants.Columns - 3 && grid[row + 1, column + 2].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row, column + 1],
                        grid[row + 1, column + 2]
                    };
            }
        }
        return null;
    }

    public static List<GameObject> CheckVertical1(int row, int column, GridSystem grid)
    {
        if (row <= GameConstants.Rows - 2)
        {
            if (grid[row, column].GetComponent<FoodItem>().
                IsSameType(grid[row + 1, column].GetComponent<FoodItem>()))
            {
                if (column >= 1 && row >= 1 && grid[row - 1, column - 1].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row + 1, column],
                        grid[row - 1, column - 1]
                    };
                if (column <= GameConstants.Columns - 2 && row >= 1 && grid[row - 1, column + 1].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row + 1, column],
                        grid[row - 1, column + 1]
                    };
            }
        }
        return null;
    }

    public static List<GameObject> CheckVertical2(int row, int column, GridSystem grid)
    {
        if (row <= GameConstants.Rows - 3)
        {
            if (grid[row, column].GetComponent<FoodItem>().
                IsSameType(grid[row + 1, column].GetComponent<FoodItem>()))
            {
                if (grid[row + 2, column].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row + 1, column],
                        grid[row + 2, column]
                    };
            }
        }
        return null;
    }

    public static List<GameObject> CheckVertical3(int row, int column, GridSystem grid)
    {
        if (row <= GameConstants.Rows - 2)
        {
            if (grid[row, column].GetComponent<FoodItem>().
                IsSameType(grid[row + 1, column].GetComponent<FoodItem>()))
            {
                if (column >= 1 && row <= GameConstants.Rows - 3 && grid[row + 2, column - 1].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row + 1, column],
                        grid[row + 2, column - 1]
                    };
                if (column <= GameConstants.Columns - 2 && row <= GameConstants.Rows - 3 && grid[row + 2, column + 1].GetComponent<FoodItem>().
                    IsSameType(grid[row, column].GetComponent<FoodItem>()))
                    return new List<GameObject>()
                    {
                        grid[row, column],
                        grid[row + 1, column],
                        grid[row + 2, column + 1]
                    };
            }
        }
        return null;
    }
}
