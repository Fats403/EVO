using TMPro;
using UnityEngine;

public class FoodPile : MonoBehaviour
{
    public int count = 10;
    public int refillPerRound = 10;
    public TextMeshProUGUI label;
    public int numPlayers = 2;
    public int baseFood = 6;

    // TODO: The current round should factor into the food pile value.

    public int Take(int amount)
    {
        int t = Mathf.Min(count, amount);
        count -= t;
        UpdateUI();
        return t;
    }

    public void RefillStartOfRound()
    {
        // Roll one D8
        int rollSum = GameManager.Instance.NextRandomInt(1, 9);
        count = baseFood + rollSum; // no carryover; discard leftovers

        UpdateUI();
    }

    public void UpdateUI()
    {
        if (label != null)
            label.text = $"Food: {count}";
    }
}
