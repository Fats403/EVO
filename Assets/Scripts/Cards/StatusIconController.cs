using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class StatusIconController : MonoBehaviour
{
    [Header("Setup")]
    public Transform iconContainer;
    public GameObject iconPrefab;

    [Header("Layout")]
    [Tooltip("Maximum number of full-size status icons before scaling them down.")]
    public int maxFullSizeIcons = 4;

    [Tooltip(
        "Scale applied when there are a few extra icons (maxFullSizeIcons+1 .. highCountThreshold)."
    )]
    public float mediumScale = 0.85f;

    [Tooltip("Scale applied when there are many icons (above highCountThreshold).")]
    public float smallScale = 0.75f;

    [Tooltip("Icon count at which we switch from mediumScale to smallScale.")]
    public int highCountThreshold = 6;

    [Header("Definitions")]
    public StatusDefinitionLibrary definitionLibrary;

    private readonly Dictionary<StatusTag, GameObject> active = new();

    public void Refresh(Creature c)
    {
        if (definitionLibrary == null || iconContainer == null || iconPrefab == null || c == null)
            return;

        var desired = new List<StatusTag>();

        // Creature-applied statuses (already distinct from dictionary keys)
        foreach (var tag in c.GetActiveStatusTags())
            desired.Add(tag);
        // Create missing icons
        foreach (var tag in desired)
        {
            if (active.ContainsKey(tag))
                continue;
            var sprite = definitionLibrary.GetIcon(tag);
            if (sprite == null)
                continue;
            var go = Object.Instantiate(iconPrefab, iconContainer);
            var img = go.GetComponent<Image>();
            if (img != null)
                img.sprite = sprite;
            active[tag] = go;
        }

        // Remove stale icons
        var toRemove = active.Keys.Where(k => !desired.Contains(k)).ToList();
        foreach (var k in toRemove)
        {
            if (active[k] != null)
                Object.Destroy(active[k]);
            active.Remove(k);
        }

        // Update tooltips and scaling now that the active set is in sync with the creature.
        UpdateIconVisuals(c);
    }

    private void UpdateIconVisuals(Creature c)
    {
        foreach (var kvp in active)
        {
            var tag = kvp.Key;
            var go = kvp.Value;
            if (go == null)
                continue;

            int count = active.Count;

            // Decide scale based on how many icons are active.
            float scale = 1f;
            if (count > maxFullSizeIcons && count <= highCountThreshold)
                scale = mediumScale;
            else if (count > highCountThreshold)
                scale = smallScale;

            // Apply uniform scale so the layout group can keep spacing logic.
            if (go.transform is RectTransform rt)
                rt.localScale = Vector3.one * scale;

            // Ensure an up-to-date tooltip exists on every icon.
            var tooltip = go.GetComponent<TooltipTrigger>();
            if (tooltip == null)
                tooltip = go.AddComponent<TooltipTrigger>();

            string displayName =
                definitionLibrary != null ? definitionLibrary.GetDisplayName(tag) : tag.ToString();
            string baseDesc =
                definitionLibrary != null ? definitionLibrary.GetDescription(tag) : string.Empty;

            int stacks = c != null ? c.GetStatus(tag) : 0;

            tooltip.title = displayName;
            tooltip.body = baseDesc;
            tooltip.useStacks = stacks > 1;
            tooltip.stacks = stacks;

            // Optional: keep tooltip icon in sync with the sprite if definitions exist.
            if (definitionLibrary != null)
            {
                var sprite = definitionLibrary.GetIcon(tag);
                if (sprite != null)
                    tooltip.icon = sprite;
            }
        }
    }
}
