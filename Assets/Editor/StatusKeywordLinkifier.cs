using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to automatically wrap status keyword occurrences in card/trait
/// description fields with TextMeshPro <link> markup, so they show tooltips.
///
/// It scans .asset files under specific data folders and, for each description string,
/// replaces matches of status display names (from StatusDefinitionLibrary) with:
///
///   <link="StatusTagName"><color=#FFD35B><b>DisplayName</b></color></link>
///
/// Notes:
/// - Uses a single highlight color for all statuses for consistency.
/// - Skips any description that already contains a <link="StatusTagName"> to keep it idempotent.
/// </summary>
public static class StatusKeywordLinkifier
{
    // Folders to scan, relative to project root.
    private static readonly string[] TargetFolders =
    {
        "Assets/Data/Cards/Effects",
        "Assets/Data/Traits/Avians",
        "Assets/Data/Traits/Herbivores",
        "Assets/Data/Traits/Carnivores",
    };

    // String properties we consider to be descriptions.
    private static readonly string[] DescriptionPropertyNames = { "description" };

    // Single highlight color for all status keywords (change if you want a different accent).
    private const string HighlightColorHex = "#FFD35B";

    [MenuItem("Tools/Cards/Linkify Status Keywords")]
    public static void LinkifyStatusKeywords()
    {
        var lib = FindStatusDefinitionLibrary();
        if (lib == null)
        {
            Debug.LogError(
                "StatusKeywordLinkifier: Could not find a StatusDefinitionLibrary asset in the project."
            );
            return;
        }

        var displayToId = BuildDisplayToIdMap(lib);
        if (displayToId.Count == 0)
        {
            Debug.LogWarning(
                "StatusKeywordLinkifier: StatusDefinitionLibrary has no entries; nothing to linkify."
            );
            return;
        }

        int totalAssets = 0;
        int modifiedAssets = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var folder in TargetFolders)
            {
                string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset == null)
                        continue;

                    totalAssets++;

                    bool changed = ProcessAsset(asset, displayToId);
                    if (changed)
                    {
                        modifiedAssets++;
                        EditorUtility.SetDirty(asset);
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log(
            $"StatusKeywordLinkifier: Processed {totalAssets} assets, modified {modifiedAssets}."
        );
    }

    private static StatusDefinitionLibrary FindStatusDefinitionLibrary()
    {
        string[] guids = AssetDatabase.FindAssets("t:StatusDefinitionLibrary");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var lib = AssetDatabase.LoadAssetAtPath<StatusDefinitionLibrary>(path);
            if (lib != null)
                return lib;
        }

        return null;
    }

    private struct KeywordPattern
    {
        public string SearchText; // Text to look for in descriptions (e.g., "Bleeding" or "Fury")
        public string Id; // StatusTag name used as link ID (e.g., "Bleeding")
        public string DisplayText; // Text to show inside the link (e.g., "Bleed" or "Damage Up")
    }

    private static List<KeywordPattern> BuildDisplayToIdMap(StatusDefinitionLibrary lib)
    {
        var patterns = new List<KeywordPattern>();
        var seen = new HashSet<string>();

        if (lib.entries == null)
            return patterns;

        foreach (var entry in lib.entries)
        {
            string id = entry.tag.ToString(); // e.g., "Bleeding" for StatusTag.Bleeding
            if (string.IsNullOrEmpty(id))
                continue;

            // Base display name from library, falling back to enum name.
            string baseDisplay = string.IsNullOrEmpty(entry.displayName) ? id : entry.displayName;

            // Helper to add a pattern if we haven't already.
            void AddPattern(string search, string display)
            {
                if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(display))
                    return;

                string key = search + "||" + id;
                if (seen.Contains(key))
                    return;

                seen.Add(key);
                patterns.Add(
                    new KeywordPattern
                    {
                        SearchText = search,
                        Id = id,
                        DisplayText = display,
                    }
                );
            }

            // Special-case overrides for nicer in-text names or legacy/raw authoring spellings.
            switch (entry.tag)
            {
                case StatusTag.Infection:
                    // Old wording: "Infected" -> new enum: Infection
                    // Display text comes from StatusDefinitionLibrary (or falls back to "Infection").
                    AddPattern("Infected", baseDisplay);
                    AddPattern("Infection", baseDisplay);
                    break;

                case StatusTag.Shield:
                    // Old wording: "Shielded" -> new enum: Shield
                    // Also fix common misspelling "Sheild".
                    AddPattern("Shielded", "Shield");
                    AddPattern("Shield", "Shield");
                    AddPattern("Sheild", "Shield");
                    break;

                case StatusTag.Fury:
                    // Normalize "Fury" references.
                    AddPattern("Fury", "Fury");
                    AddPattern("Fury +1", "Fury");
                    break;

                case StatusTag.NoForage:
                    // Normalize "NoForage" and "No Forage" to "No Forage".
                    AddPattern("NoForage", "No Forage");
                    AddPattern("No Forage", "No Forage");
                    break;

                case StatusTag.Bleed:
                    // Old wording: "Bleeding" -> new enum: Bleed
                    // Normalize "Bleeding" and "Bleed" to "Bleed".
                    AddPattern("Bleeding", "Bleed");
                    AddPattern("Bleed", "Bleed");
                    break;

                case StatusTag.Fatigue:
                    // Old wording: "Fatigued" -> new enum: Fatigue
                    // Normalize "Fatigued" and "Fatigue" to "Fatigue".
                    AddPattern("Fatigued", "Fatigue");
                    AddPattern("Fatigue", "Fatigue");
                    break;

                case StatusTag.Stun:
                    // Old wording: "Stunned" -> new enum: Stun
                    AddPattern("Stunned", baseDisplay);
                    AddPattern("Stun", baseDisplay);
                    break;

                case StatusTag.Suppress:
                    // Old wording: "Suppressed" -> new enum: Suppress
                    AddPattern("Suppressed", baseDisplay);
                    AddPattern("Suppress", baseDisplay);
                    break;

                case StatusTag.Malnourish:
                    // Old wording: "Malnourished" -> new enum: Malnourish
                    AddPattern("Malnourished", baseDisplay);
                    AddPattern("Malnourish", baseDisplay);
                    break;

                default:
                    // Default patterns:
                    // - Match the display name (what players see)
                    // - Also match the raw enum ID if it's different (legacy/raw authoring)
                    AddPattern(baseDisplay, baseDisplay);
                    if (!string.Equals(baseDisplay, id))
                        AddPattern(id, baseDisplay);
                    break;
            }
        }

        // Prefer longer search strings first when scanning text (avoid partial ordering surprises).
        patterns.Sort((a, b) => b.SearchText.Length.CompareTo(a.SearchText.Length));
        return patterns;
    }

    private static bool ProcessAsset(ScriptableObject asset, List<KeywordPattern> patterns)
    {
        bool modified = false;
        var so = new SerializedObject(asset);

        foreach (var propName in DescriptionPropertyNames)
        {
            var prop = so.FindProperty(propName);
            if (prop == null || prop.propertyType != SerializedPropertyType.String)
                continue;

            string original = prop.stringValue;
            string updated = LinkifyDescription(original, patterns);

            if (!string.Equals(original, updated))
            {
                prop.stringValue = updated;
                modified = true;
            }
        }

        if (modified)
            so.ApplyModifiedProperties();

        return modified;
    }

    private static string LinkifyDescription(string text, List<KeywordPattern> patterns)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Idempotency: if we've already linkified any keyword in this description, skip it.
        // (This prevents nesting <link> tags or repeatedly reformatting text.)
        if (text.Contains("<link="))
            return text;

        // Canonical display name per status ID (Bleed, Damage Up, Fatigue, etc.).
        var idToDisplay = new Dictionary<string, string>();
        foreach (var p in patterns)
        {
            if (string.IsNullOrEmpty(p.Id) || string.IsNullOrEmpty(p.DisplayText))
                continue;
            if (!idToDisplay.ContainsKey(p.Id))
                idToDisplay[p.Id] = p.DisplayText;
        }

        // Phase 1: Replace matches with numeric placeholders so we *cannot* re-match inside
        // placeholders (the previous implementation embedded the keyword text and broke stacks).
        var tokens = new List<(string id, int stacks)>();
        string result = text;

        foreach (var p in patterns)
        {
            string search = p.SearchText;
            string id = p.Id;
            if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(id))
                continue;

            // "StatusName (X)" -> placeholder, capturing X (stack count)
            // Example: "Infected (2)" => "<<KW:12>>" later becomes "+2 Infected".
            string parenPattern = $@"\b{Regex.Escape(search)}\s*\((\d+)\)";
            result = Regex.Replace(
                result,
                parenPattern,
                m =>
                {
                    int stacks = 0;
                    int.TryParse(m.Groups[1].Value, out stacks);
                    stacks = Mathf.Max(0, stacks);
                    int tokenIndex = tokens.Count;
                    tokens.Add((id, stacks));
                    return $"<<KW:{tokenIndex}>>";
                }
            );

            // "+X StatusName" -> placeholder, capturing X (stack count)
            // Example: "+2 Infected" => "<<KW:12>>" later becomes "+2 Infected".
            string plusPattern = $@"\+(\d+)\s+{Regex.Escape(search)}\b";
            result = Regex.Replace(
                result,
                plusPattern,
                m =>
                {
                    int stacks = 0;
                    int.TryParse(m.Groups[1].Value, out stacks);
                    stacks = Mathf.Max(0, stacks);
                    int tokenIndex = tokens.Count;
                    tokens.Add((id, stacks));
                    return $"<<KW:{tokenIndex}>>";
                }
            );

            // Plain "StatusName" -> placeholder with stacks=0.
            string wordPattern = $@"\b{Regex.Escape(search)}\b";
            result = Regex.Replace(
                result,
                wordPattern,
                m =>
                {
                    int tokenIndex = tokens.Count;
                    tokens.Add((id, 0));
                    return $"<<KW:{tokenIndex}>>";
                }
            );
        }

        // Phase 2: Turn placeholders into TMP <link> markup.
        result = Regex.Replace(
            result,
            "<<KW:(\\d+)>>",
            m =>
            {
                int tokenIndex;
                if (!int.TryParse(m.Groups[1].Value, out tokenIndex))
                    return m.Value;
                if (tokenIndex < 0 || tokenIndex >= tokens.Count)
                    return m.Value;

                var token = tokens[tokenIndex];
                string id = token.id;
                int stacks = token.stacks;

                string display;
                if (!idToDisplay.TryGetValue(id, out display) || string.IsNullOrEmpty(display))
                    display = id;

                // Required formatting:
                // - "Status (X)" -> "+X Status"
                // - "+X Status" stays "+X Status"
                // and the entire "+X Status" is highlighted.
                string label = stacks > 0 ? $"+{stacks} {display}" : display;
                return $"<link=\"{id}\"><color={HighlightColorHex}><b>{label}</b></color></link>";
            }
        );

        return result;
    }
}
