using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class CardUI : MonoBehaviour
{
    [Header("UI References")]
    public Image backgroundImage;
    public Image artworkImage;
    public TMP_Text nameText;
    public TMP_Text statsText;

    [HideInInspector] public CardData Data;

    private Action<CardUI> onClick;

    public void Initialize(CardData data, Action<CardUI> onClickCallback)
    {
        Data = data;
        onClick = onClickCallback;

        if (nameText != null) nameText.text = data.cardName;
        if (statsText != null) statsText.text = $"Size: {data.size} | Speed: {data.speed} | Tier: {data.tier}";
        if (artworkImage != null) artworkImage.sprite = data.artwork;

        // Use background based on card type (Herbivore, Carnivore, Avian)
        if (backgroundImage != null)
        {
            backgroundImage.sprite = data.background;
        }

        // Attach click listener
        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(this));
        }
    }
}
