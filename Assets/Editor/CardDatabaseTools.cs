#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utilities for managing the CardDatabase asset.
/// - Scans the project for all CardDefinition assets and populates the database.
/// </summary>
public static class CardDatabaseTools
{
    [MenuItem("Tools/Cards/Populate CardDatabase (All CardDefinitions)")]
    public static void PopulateCardDatabase()
    {
        // Find or create the CardDatabase asset.
        CardDatabase db = FindOrCreateDatabaseAsset();
        if (db == null)
        {
            Debug.LogError("CardDatabaseTools: Failed to create/find CardDatabase asset.");
            return;
        }

        var all = new List<CardDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:CardDefinition");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            if (card != null && !all.Contains(card))
                all.Add(card);
        }

        Undo.RecordObject(db, "Populate CardDatabase");
        db.allCards = all;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        Debug.Log($"CardDatabaseTools: Populated CardDatabase with {all.Count} cards.");
    }

    private static CardDatabase FindOrCreateDatabaseAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:CardDatabase");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<CardDatabase>(path);
        }

        // If none exists, create one in a sensible default location.
        const string assetPath = "Assets/Data/CardDatabase.asset";
        var db = ScriptableObject.CreateInstance<CardDatabase>();
        AssetDatabase.CreateAsset(db, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"CardDatabaseTools: Created new CardDatabase at {assetPath}");
        return db;
    }
}
#endif






