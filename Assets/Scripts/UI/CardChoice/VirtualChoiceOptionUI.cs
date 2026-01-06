using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for displaying a VirtualChoiceOption.
/// This is a simplified card-like display with title, description, and optional icon.
/// </summary>
public class VirtualChoiceOptionUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text displaying the option title.")]
    public TMP_Text titleText;

    [Tooltip("Text displaying the option description.")]
    public TMP_Text descriptionText;

    [Tooltip("Optional icon/artwork image.")]
    public Image iconImage;

    [Tooltip("Optional background image to tint.")]
    public Image backgroundImage;

    [Header("Layout")]
    [Tooltip("If true, hide the icon area when no icon is provided.")]
    public bool hideIconWhenEmpty = true;

    [Tooltip("Parent object containing the icon (hidden when no icon).")]
    public GameObject iconContainer;

    [Header("Icon Types")]
    [Tooltip("Default icon used when iconType is Default or when a specific icon is missing.")]
    public Sprite defaultIcon;

    [Tooltip("Icon for Attack type choices.")]
    public Sprite attackIcon;

    [Tooltip("Icon for Defense type choices.")]
    public Sprite defenseIcon;

    [Tooltip("Icon for Buff type choices.")]
    public Sprite buffIcon;

    [Tooltip("Icon for Debuff type choices.")]
    public Sprite debuffIcon;

    [Tooltip("Icon for Special type choices.")]
    public Sprite specialIcon;

    /// <summary>
    /// Initialize this UI with a VirtualChoiceOption.
    /// </summary>
    public void Initialize(VirtualChoiceOption option)
    {
        if (option == null)
        {
            Debug.LogWarning("VirtualChoiceOptionUI: Received null option.");
            return;
        }

        // Set title
        if (titleText != null)
        {
            titleText.text = option.title ?? "";
        }

        // Set description
        if (descriptionText != null)
        {
            descriptionText.text = option.description ?? "";
        }

        // Resolve the icon based on iconType
        Sprite resolvedIcon = GetIconForType(option.iconType, option.customIcon);
        bool hasIcon = resolvedIcon != null;

        if (iconImage != null)
        {
            iconImage.sprite = resolvedIcon;
            iconImage.enabled = hasIcon;
        }

        // Hide icon container if no icon
        if (hideIconWhenEmpty && iconContainer != null)
        {
            iconContainer.SetActive(hasIcon);
        }

        // Apply background color
        if (backgroundImage != null)
        {
            backgroundImage.color = option.backgroundColor;
        }
    }

    /// <summary>
    /// Returns the appropriate sprite for the given icon type.
    /// Falls back to defaultIcon if the specific icon is not assigned.
    /// </summary>
    private Sprite GetIconForType(VirtualChoiceIconType iconType, Sprite customSprite = null)
    {
        Sprite result = iconType switch
        {
            VirtualChoiceIconType.Attack => attackIcon,
            VirtualChoiceIconType.Defense => defenseIcon,
            VirtualChoiceIconType.Buff => buffIcon,
            VirtualChoiceIconType.Debuff => debuffIcon,
            VirtualChoiceIconType.Special => specialIcon,
            VirtualChoiceIconType.Custom => customSprite,
            _ => defaultIcon, // Default and any unknown types
        };

        // Fall back to default if the specific icon isn't assigned
        return result != null ? result : defaultIcon;
    }
}
