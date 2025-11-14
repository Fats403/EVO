using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CreatureCardUI : BaseCardUI
{
    public CreatureCard Data { get; private set; }

    [Header("UI References")]
    public Image artworkImage;
    public Image backgroundImage;
    public TMP_Text nameText;
    public TMP_Text tierText;
    public TMP_Text speedText;
    public TMP_Text bodyText;
    public TMP_Text healthText;
    public TMP_Text traitDescText;

    protected BoardSlot hoverSlot;

    public void Initialize(CreatureCard data)
    {
        Data = data;

        if (nameText != null)
            nameText.text = data.cardName;
        if (tierText != null)
            tierText.text = $"Tier: {data.tier}";
        if (speedText != null)
            speedText.text = $"Speed: {data.speed}";
        if (bodyText != null)
            bodyText.text = $"Size: {data.size}";
        if (healthText != null)
            healthText.text = $"{data.maxHealth}";
        if (artworkImage != null)
            artworkImage.sprite = data.artwork;
        if (backgroundImage != null)
            backgroundImage.sprite = data.background;

        if (traitDescText != null)
        {
            string traitLine = "";
            if (data.baseTraits != null && data.baseTraits.Length > 0 && data.baseTraits[0] != null)
            {
                var t = data.baseTraits[0];
                string tName = string.IsNullOrEmpty(t.traitName) ? t.name : t.traitName;
                if (!string.IsNullOrEmpty(t.description))
                    traitLine = $"{tName}: {t.description}";
                else
                    traitLine = tName;
            }
            traitDescText.text = traitLine;
        }
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);
        UpdateHoverSlot(eventData.position);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;
        isDragging = false;
        // Find closest board slot (world-space)
        BoardSlot closest = FindClosestSlot(eventData.position);
        bool placed = false;
        if (
            closest != null
            && Vector2.Distance(closest.ScreenPosition, eventData.position) < highlightRadius
        )
        {
            if (closest.owner == SlotOwner.Player1)
            {
                if (
                    GameManager.Instance != null
                    && GameManager.Instance.currentPhase == GamePhase.Place
                )
                {
                    // Check global rules (era, tier, momentum) before spawning
                    string reason;
                    if (
                        GameManager.Instance.CanPlayCreatureCard(
                            Data,
                            SlotOwner.Player1,
                            out reason
                        )
                    )
                    {
                        placed = DeckManager.Instance.SpawnCreature(Data, closest);
                        if (!placed && !string.IsNullOrEmpty(reason))
                        {
                            // If spawn failed for some reason, refund the momentum we just spent
                            // (best-effort: simply add cost back based on card)
                            int cost = GameManager.Instance.GetCreatureCost(Data);
                            if (cost > 0)
                            {
                                // Manual refund since TrySpendMomentum only subtracts
                                if (GameManager.Instance != null)
                                {
                                    GameManager.Instance.p1Momentum += cost;
                                    GameManager.Instance.UpdateMomentumUI();
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(reason))
                        {
                            FeedbackManager.Instance?.Log(reason);
                        }
                    }
                }
            }
        }

        if (placed)
        {
            handLayout?.NotifyDragEnd(this, false);
            if (hoverSlot != null)
            {
                hoverSlot.HideHoverIndicator();
                hoverSlot = null;
            }
            Destroy(gameObject);
        }
        else
        {
            ReturnToHand();
            if (hoverSlot != null)
            {
                hoverSlot.HideHoverIndicator();
                hoverSlot = null;
            }
        }
    }

    private BoardSlot FindClosestSlot(Vector2 screenPos)
    {
        BoardSlot[] slots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
        float best = float.MaxValue;
        BoardSlot nearest = null;

        foreach (var s in slots)
        {
            float d = Vector2.Distance(s.ScreenPosition, screenPos);
            if (d < best)
            {
                best = d;
                nearest = s;
            }
        }

        return nearest;
    }

    protected virtual void UpdateHoverSlot(Vector2 screenPointer)
    {
        bool canPlaceNow =
            GameManager.Instance != null && GameManager.Instance.currentPhase == GamePhase.Place;
        if (!canPlaceNow)
        {
            if (hoverSlot != null)
            {
                hoverSlot.HideHoverIndicator();
                hoverSlot = null;
            }
            return;
        }

        BoardSlot closest = FindClosestSlot(screenPointer);
        if (closest == null)
        {
            if (hoverSlot != null)
            {
                hoverSlot.HideHoverIndicator();
                hoverSlot = null;
            }
            return;
        }

        float dist = Vector2.Distance(closest.ScreenPosition, screenPointer);
        if (dist > highlightRadius || closest.owner != SlotOwner.Player1 || closest.occupied)
        {
            if (hoverSlot != null)
            {
                hoverSlot.HideHoverIndicator();
                hoverSlot = null;
            }
            return;
        }

        if (hoverSlot != null && hoverSlot != closest)
        {
            hoverSlot.HideHoverIndicator();
        }

        if (closest != hoverSlot)
        {
            var prefab = DeckManager.Instance?.hoverIndicatorPrefab;
            closest.ShowHoverIndicator(prefab);
            hoverSlot = closest;
        }
    }
}
