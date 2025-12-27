using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    public static int player1;
    public static int player2;

    public TMP_Text p1Label;
    public TMP_Text p2Label;

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

    public void UpdateUI()
    {
        if (p1Label != null)
            p1Label.text = $"{player1}";
        if (p2Label != null)
            p2Label.text = $"{player2}";
    }
}
