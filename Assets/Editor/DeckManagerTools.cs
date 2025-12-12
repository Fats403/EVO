#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to auto-populate DeckManager.allCards from card assets
/// in the project. Run via the Tools menu whenever you add new cards.
/// </summary>
public static class DeckManagerTools
{
    private static readonly string[] CreatureSearchPaths = new[]
    {
        "Assets/Data/Cards/Creatures/Herbivores",
        "Assets/Data/Cards/Creatures/Carnivores",
        "Assets/Data/Cards/Creatures/Avians",
    };

    private static readonly string[] EffectSearchPaths = new[] { "Assets/Data/Cards/Effects" };

    [MenuItem("Tools/Decks/Populate DeckManager allCards")]
    public static void PopulateAllCards()
    {
        // Find the DeckManager in the active scene
        DeckManager dm = Object.FindFirstObjectByType<DeckManager>();
        if (dm == null)
        {
            Debug.LogError("DeckManagerTools: No DeckManager found in the active scene.");
            return;
        }

        var all = new List<ScriptableObject>();

        // Creatures
        foreach (var path in CreatureSearchPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:CreatureCard", new[] { path });
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (card != null && !all.Contains(card))
                    all.Add(card);
            }
        }

        // Effects
        foreach (var path in EffectSearchPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:EffectCard", new[] { path });
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (card != null && !all.Contains(card))
                    all.Add(card);
            }
        }

        Undo.RecordObject(dm, "Populate DeckManager allCards");
        dm.allCards = all;
        EditorUtility.SetDirty(dm);

        Debug.Log($"DeckManagerTools: Populated DeckManager.allCards with {all.Count} cards.");
    }
}
#endif



