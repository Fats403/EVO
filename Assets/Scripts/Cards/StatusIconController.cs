using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class StatusIconController : MonoBehaviour
{
    [Header("Setup")]
    public Transform iconContainer;
    public GameObject iconPrefab;

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

            // Attach generic tooltip trigger so hovering the icon shows its name + description.
            var tooltip = go.GetComponent<TooltipTrigger>();
            if (tooltip == null)
                tooltip = go.AddComponent<TooltipTrigger>();

            if (definitionLibrary != null)
            {
                tooltip.title = definitionLibrary.GetDisplayName(tag);
                tooltip.body = definitionLibrary.GetDescription(tag);
            }
            else
            {
                tooltip.title = tag.ToString();
                tooltip.body = string.Empty;
            }

            tooltip.icon = sprite;
        }

        // Remove stale icons
        var toRemove = active.Keys.Where(k => !desired.Contains(k)).ToList();
        foreach (var k in toRemove)
        {
            if (active[k] != null)
                Object.Destroy(active[k]);
            active.Remove(k);
        }
    }
}
