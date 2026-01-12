using System;
using UnityEngine;

/// <summary>
/// Predefined icon types for virtual choice options.
/// The UI component holds references to sprites for each type.
/// </summary>
public enum VirtualChoiceIconType
{
    Default = 0, // Uses the default icon wired in Unity
    Attack = 1, // Offensive/damage themed
    Defense = 2, // Defensive/protection themed
    Buff = 3, // Positive status/enhancement themed
    Debuff = 4, // Negative status/weakening themed
    Special = 5, // Special/unique ability themed
    Custom = 99, // Uses the custom sprite passed in directly
}

/// <summary>
/// A "virtual card" option for use with CardChoiceManager.
/// This isn't a real game card, but rather a choice option with a title, description,
/// and optional icon that can trigger custom effects when selected.
///
/// Usage:
///   // Create options at runtime:
///   var option1 = VirtualChoiceOption.Create("Draw 2 Cards", "Draw 2 cards from your deck.", VirtualChoiceIconType.Buff);
///   var option2 = VirtualChoiceOption.Create("Deal Damage", "Deal 5 damage.", VirtualChoiceIconType.Attack);
///
///   // Or create asset-based options in the editor for reuse
/// </summary>
[CreateAssetMenu(menuName = "Cards/Virtual Choice Option")]
public class VirtualChoiceOption : ScriptableObject
{
    [Header("Display")]
    [Tooltip("The title shown on this choice option.")]
    public string title;

    [Tooltip("Description explaining what this choice does.")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Which predefined icon to use. Set to Custom to use a specific sprite.")]
    public VirtualChoiceIconType iconType = VirtualChoiceIconType.Default;

    [Tooltip("Custom icon sprite. Only used when iconType is set to Custom.")]
    public Sprite customIcon;

    [Tooltip("Optional background color tint for this choice. White = no tint.")]
    public Color backgroundColor = Color.white;

    [Header("Identification")]
    [Tooltip("Unique identifier for this option. Used for callbacks and networking.")]
    public string optionId;

    [Tooltip("Optional: Arbitrary payload data that can be checked by the effect handler.")]
    public string payload;

    /// <summary>
    /// Runtime-only callback that fires when this specific option is selected.
    /// Note: This is set at runtime and won't serialize. Use optionId for persistent behavior.
    /// </summary>
    [NonSerialized]
    public Action onSelected;

    /// <summary>
    /// Creates a runtime VirtualChoiceOption instance (not saved as an asset).
    /// Useful for dynamically generated choices.
    /// </summary>
    public static VirtualChoiceOption Create(
        string title,
        string description,
        VirtualChoiceIconType iconType = VirtualChoiceIconType.Default,
        string optionId = null,
        Action onSelected = null,
        string payload = null,
        Color? backgroundColor = null,
        Sprite customIcon = null
    )
    {
        var option = CreateInstance<VirtualChoiceOption>();
        option.title = title;
        option.description = description;
        option.iconType = iconType;
        option.customIcon = customIcon;
        option.optionId = optionId ?? Guid.NewGuid().ToString();
        option.onSelected = onSelected;
        option.payload = payload;
        option.backgroundColor = backgroundColor ?? Color.white;
        return option;
    }

    /// <summary>
    /// Creates a runtime VirtualChoiceOption with a custom sprite icon.
    /// Convenience overload for when you have a specific sprite to use.
    /// </summary>
    public static VirtualChoiceOption CreateWithCustomIcon(
        string title,
        string description,
        Sprite icon,
        string optionId = null,
        Action onSelected = null,
        string payload = null,
        Color? backgroundColor = null
    )
    {
        return Create(
            title,
            description,
            VirtualChoiceIconType.Custom,
            optionId,
            onSelected,
            payload,
            backgroundColor,
            icon
        );
    }

    /// <summary>
    /// Creates a copy of this option with a runtime callback attached.
    /// Useful when you have an asset-based option but want to attach a specific callback.
    /// </summary>
    public VirtualChoiceOption WithCallback(Action callback)
    {
        var copy = Create(
            title,
            description,
            iconType,
            optionId,
            callback,
            payload,
            backgroundColor,
            customIcon
        );
        return copy;
    }

    /// <summary>
    /// Returns the optionId, falling back to the asset name if optionId is empty.
    /// </summary>
    public string GetId()
    {
        return string.IsNullOrEmpty(optionId) ? name : optionId;
    }
}
