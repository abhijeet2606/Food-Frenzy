using System;
using UnityEngine;

public class FoodItem : MonoBehaviour
{
    public BonusType Bonus { get; set; }
    public int Column { get; set; }
    public int Row { get; set; }
    public string Type { get; set; }

    public FoodItem()
    {
        Bonus = BonusType.None;
    }

    public bool IsSameType(FoodItem otherShape)
    {
        if (otherShape == null || !(otherShape is FoodItem))
            throw new ArgumentException("otherShape");

        return string.Compare(this.Type, otherShape.Type) == 0;
    }

    public void Assign(string type, int row, int column)
    {
        if (string.IsNullOrEmpty(type))
            throw new ArgumentException("type");

        Column = column;
        Row = row;
        Type = type;
    }

    public static void SwapColumnRow(FoodItem a, FoodItem b)
    {
        int temp = a.Row;
        a.Row = b.Row;
        b.Row = temp;

        temp = a.Column;
        a.Column = b.Column;
        b.Column = temp;
    }
}
