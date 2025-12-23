using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data for a single keyword tooltip.
/// Designers author these once and reference them from text via a link ID.
/// </summary>
[Serializable]
public class KeywordTooltipDefinition
{
    [Tooltip("ID used from TMP <link=\"...\"> markup, e.g. <link=\"frenzy\">Frenzy</link>.")]
    public string id;

    [Tooltip("Title line shown in the tooltip header.")]
    public string title;

    [TextArea]
    [Tooltip("Body text describing the keyword's effect.")]
    public string body;

    [Tooltip("Optional icon shown in the tooltip.")]
    public Sprite icon;

    [Header("Optional Stacks Display")]
    [Tooltip("If true, tooltip will show a stacks readout using TooltipManager.stacksText.")]
    public bool useStacks;

    [Tooltip("Default stacks value to display (can be overridden by callers if needed).")]
    public int defaultStacks = 1;
}

/// <summary>
/// Central registry of keyword tooltips.
/// Place an instance in a Resources folder (e.g. Resources/KeywordTooltipLibrary.asset)
/// so it can be loaded at runtime.
/// </summary>
[CreateAssetMenu(menuName = "EVO/Keyword Tooltip Library", fileName = "KeywordTooltipLibrary")]
public class KeywordTooltipLibrary : ScriptableObject
{
    [Tooltip("List of all keyword tooltip definitions available in the game.")]
    public KeywordTooltipDefinition[] keywords;

    private Dictionary<string, KeywordTooltipDefinition> lookup;

    private static KeywordTooltipLibrary _instance;

    /// <summary>
    /// Global access to the keyword library. Returns null if no asset was found.
    /// </summary>
    public static KeywordTooltipLibrary Instance
    {
        get
        {
            if (_instance == null)
            {
                // Asset should live at Resources/KeywordTooltipLibrary.asset
                _instance = Resources.Load<KeywordTooltipLibrary>("KeywordTooltipLibrary");
            }

            return _instance;
        }
    }

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<string, KeywordTooltipDefinition>(StringComparer.OrdinalIgnoreCase);

        if (keywords == null)
            return;

        foreach (var def in keywords)
        {
            if (def == null)
                continue;

            if (string.IsNullOrWhiteSpace(def.id))
                continue;

            // Later entries with the same ID win; this makes it easy to override.
            lookup[def.id.Trim()] = def;
        }
    }

    /// <summary>
    /// Try to fetch a tooltip definition by keyword ID.
    /// </summary>
    public bool TryGet(string id, out KeywordTooltipDefinition def)
    {
        def = null;

        if (string.IsNullOrEmpty(id))
            return false;

        if (lookup == null || lookup.Count == 0)
            BuildLookup();

        return lookup != null && lookup.TryGetValue(id, out def);
    }
}
