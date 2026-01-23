using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct GridPosition
{
    public int Column;
    public int Row;
}

public class AlteredFoodInfo
{
    private List<GameObject> newFood { get; set; }
    public int MaxDistance { get; set; }

    public IEnumerable<GameObject> AlteredFood
    {
        get { return newFood.Distinct(); }
    }

    public void AddFood(GameObject go)
    {
        if (!newFood.Contains(go))
            newFood.Add(go);
    }

    public AlteredFoodInfo()
    {
        newFood = new List<GameObject>();
    }
}

public class MatchesInfo
{
    private List<GameObject> matchedFood;

    public IEnumerable<GameObject> MatchedFood
    {
        get { return matchedFood.Distinct(); }
    }

    public void AddObject(GameObject go)
    {
        if (!matchedFood.Contains(go))
            matchedFood.Add(go);
    }

    public void AddObjectRange(IEnumerable<GameObject> gos)
    {
        foreach (var item in gos)
        {
            AddObject(item);
        }
    }

    public MatchesInfo()
    {
        matchedFood = new List<GameObject>();
        BonusesContained = BonusType.None;
    }

    public BonusType BonusesContained { get; set; }
}
