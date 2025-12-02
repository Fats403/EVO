using TMPro;
using UnityEngine;

public class FoodPile : MonoBehaviour
{
    public int count = 0;
    public TextMeshProUGUI label;

    [Tooltip("How many players the food calculation should assume (used to scale for >2 players).")]
    public int numPlayers = 2;

    [Header("Base & Scaling")]
    [Tooltip(
        "Baseline total food for a 2‑player Triassic round 1 before era/round bonuses and variance."
    )]
    public int baseFood = 4;

    [Tooltip("Extra food added per player above 2 (e.g., 3 => +3 food for 3P, +6 for 4P).")]
    public int perExtraPlayerFood = 3;

    [Tooltip("Maximum extra food gained from being later within an era (0–2).")]
    public int maxRoundBonusPerEra = 2;

    [Header("Randomness")]
    [Tooltip("Minimum random variance added each round.")]
    public int varianceMin = 0;

    [Tooltip("Maximum random variance added each round (inclusive).")]
    public int varianceMax = 4;

    [Header("Debug / UX")]
    [Tooltip("If true, logs a breakdown of how this round's food total was computed.")]
    public bool showFoodBreakdownInLog = true;

    [Tooltip("If true, shows floating text at the food pile with this round's total.")]
    public bool showFoodFloatingText = false;

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
        int round = 1;
        Era era = Era.Triassic;

        if (GameManager.Instance != null)
        {
            round = Mathf.Max(1, GameManager.Instance.currentRound);
            era = GameManager.Instance.currentEra;
        }

        // Era-based scaling: later eras tend to support (and demand) more food.
        int eraBonus = GetEraFoodBonus(era);

        // Within each era, food ramps up slightly as rounds progress (up to maxRoundBonusPerEra).
        int roundBonus = GetRoundBonusWithinEra(round, era);

        // Deterministic randomness via GameManager RNG; falls back to Unity's RNG if needed.
        int variance = 0;
        int vMin = Mathf.Min(varianceMin, varianceMax);
        int vMax = Mathf.Max(varianceMin, varianceMax) + 1; // NextRandomInt max is exclusive

        if (GameManager.Instance != null)
        {
            variance = GameManager.Instance.NextRandomInt(vMin, vMax);
        }

        // Slight adjustment for games with more than 2 players.
        int extraPlayers = Mathf.Max(0, numPlayers - 2);
        int playerBonus = extraPlayers * Mathf.Max(0, perExtraPlayerFood);

        int totalFood = baseFood + eraBonus + roundBonus + variance + playerBonus;
        count = Mathf.Max(0, totalFood); // no carryover; discard leftovers

        UpdateUI();

        // Optional: explain to the player how we arrived at this round's food total.
        if (FeedbackManager.Instance != null)
        {
            if (showFoodBreakdownInLog)
            {
                string breakdown =
                    $"Food this round: {totalFood} "
                    + $"(base {baseFood}"
                    + $", era +{eraBonus} [{era}]"
                    + $", round +{roundBonus}"
                    + $", roll +{variance}"
                    + (playerBonus > 0 ? $", players +{playerBonus}" : string.Empty)
                    + ")";
                FeedbackManager.Instance.Log(breakdown);
            }

            if (showFoodFloatingText)
            {
                FeedbackManager.Instance.ShowFloatingText(
                    $"Food: {totalFood}",
                    transform.position,
                    new Color(0.9f, 0.95f, 0.6f)
                );
            }
        }
    }

    int GetEraFoodBonus(Era era)
    {
        // Tuned so Triassic is lean, then ramps into a richer mid/late game.
        switch (era)
        {
            case Era.Triassic:
                return 0;
            case Era.Jurassic:
                return 2;
            case Era.Cretaceous:
                return 4;
            case Era.Extinction:
                // Strongest baseline; extinction pressure still comes from combat/weather.
                return 6;
            default:
                return 0;
        }
    }

    int GetRoundBonusWithinEra(int round, Era era)
    {
        // Map global round index into a 0–N index within the current era.
        int firstRoundOfEra;
        switch (era)
        {
            case Era.Triassic:
                firstRoundOfEra = 1;
                break;
            case Era.Jurassic:
                firstRoundOfEra = 5;
                break;
            case Era.Cretaceous:
                firstRoundOfEra = 9;
                break;
            case Era.Extinction:
                firstRoundOfEra = 13;
                break;
            default:
                firstRoundOfEra = 1;
                break;
        }

        int idx = Mathf.Max(0, round - firstRoundOfEra);
        // Cap so each era gives at most maxRoundBonusPerEra extra food from being later in that era.
        return Mathf.Clamp(idx, 0, Mathf.Max(0, maxRoundBonusPerEra));
    }

    public void UpdateUI()
    {
        if (label != null)
            label.text = $"Food: {count}";
    }
}
