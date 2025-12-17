using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility to generate stable card IDs for all CreatureCard and EffectCard assets
/// that currently have an empty cardId field.
///
/// Usage:
/// - From the Unity menu, run Tools → Cards → Generate Missing Card IDs.
/// - It will scan the project for CreatureCard / EffectCard assets and fill cardId based on
///   the asset name (e.g., "Raptor Ambush" -> "CRE_RAPTOR_AMBUSH").
/// - Existing non-empty IDs are left untouched so you can manually curate them later.
/// </summary>
public static class CardIdGenerator
{
    [MenuItem("Tools/Cards/Generate Missing Card IDs")]
    public static void GenerateMissingIds()
    {
        GenerateForType<CreatureCard>("CRE");
        GenerateForType<EffectCard>("EFF");

        AssetDatabase.SaveAssets();
        Debug.Log("CardIdGenerator: Finished generating missing card IDs.");
    }

    private static void GenerateForType<T>(string prefix)
        where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                continue;

            var so = new SerializedObject(asset);
            var idProp = so.FindProperty("cardId");
            if (idProp == null)
                continue;

            if (!string.IsNullOrEmpty(idProp.stringValue))
                continue; // Respect existing IDs

            // Build a readable, stable ID from the asset file name.
            // Example: "NewCreature_Raptor Ambush" -> "CRE_NEWCREATURE_RAPTOR_AMBUSH"
            var fileName = Path.GetFileNameWithoutExtension(path);
            var safeName = fileName.ToUpperInvariant().Replace(" ", "_");
            var generatedId = $"{prefix}_{safeName}";

            idProp.stringValue = generatedId;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            Debug.Log($"CardIdGenerator: Set ID '{generatedId}' on {typeof(T).Name} at {path}");
        }
    }
}
