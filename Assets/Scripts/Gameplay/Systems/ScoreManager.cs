using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    public static int player1;
    public static int player2;

    public TMP_Text localScoreLabel;
    public TMP_Text opponentScoreLabel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    private void Start()
    {
        Reset();
    }

    public void Reset()
    {
        player1 = 0;
        player2 = 0;
        UpdateUI();
    }

    public void Add(SlotOwner owner, int amount)
    {
        if (owner == SlotOwner.Player1)
            player1 += amount;
        else
            player2 += amount;
        UpdateUI();
    }

    /// <summary>
    /// Returns the score for the given owner (Player1 / Player2).
    /// </summary>
    public static int GetScore(SlotOwner owner)
    {
        return owner == SlotOwner.Player1 ? player1 : player2;
    }

    /// <summary>
    /// Convenience accessor for the local player's score, taking into account
    /// networked games where the local player may be Player2.
    /// </summary>
    public static int LocalScore => GetScore(NetworkRoleHelper.LocalRole);

    /// <summary>
    /// Convenience accessor for the opponent's score, taking into account
    /// networked games where the local player may be Player2.
    /// </summary>
    public static int OpponentScore => GetScore(NetworkRoleHelper.RemoteRole);

    public void UpdateUI()
    {
        // In offline games, LocalRole is always Player1, so this behaves
        // like the old P1/P2 mapping. In networked games, LocalRole may
        // be Player2, so we map scores to "local" (bottom HUD) and
        // "opponent" (top HUD) using NetworkRoleHelper.
        int localScore = LocalScore;
        int opponentScore = OpponentScore;

        if (localScoreLabel != null)
            localScoreLabel.text = $"{localScore}";
        if (opponentScoreLabel != null)
            opponentScoreLabel.text = $"{opponentScore}";
    }
}
