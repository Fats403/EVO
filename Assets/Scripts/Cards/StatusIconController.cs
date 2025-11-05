using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class StatusIconController : MonoBehaviour
{
    [Header("Setup")]
    public StatusIconLibrary library;
    public Transform iconContainer;   // RectTransform under the creature
    public GameObject iconPrefab;     // Prefab with an Image component

    private readonly Dictionary<StatusTag, GameObject> active = new();

    public void Refresh(Creature c)
    {
        if (library == null || iconContainer == null || iconPrefab == null || c == null) return;

        var desired = new List<StatusTag>();

        // Intrinsic: fatigue inferred from Creature
        if (c.fatigueStacks > 0) desired.Add(StatusTag.Fatigued);

        // Effect-driven tags from traits
        if (c.traits != null)
        {
            foreach (var t in c.traits)
            {
                if (t == null) continue;
                t.CollectStatusTags(c, desired);
            }
        }

        desired = desired.Distinct().ToList();

        // Create missing icons
        foreach (var tag in desired)
        {
            if (active.ContainsKey(tag)) continue;
            var sprite = library.Get(tag);
            if (sprite == null) continue;
            var go = Object.Instantiate(iconPrefab, iconContainer);
            var img = go.GetComponent<Image>();
            if (img != null) img.sprite = sprite;
            active[tag] = go;
        }

        // Remove stale icons
        var toRemove = active.Keys.Where(k => !desired.Contains(k)).ToList();
        foreach (var k in toRemove)
        {
            if (active[k] != null) Object.Destroy(active[k]);
            active.Remove(k);
        }
    }
}


