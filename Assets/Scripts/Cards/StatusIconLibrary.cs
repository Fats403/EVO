using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Status Icon Library")]
public class StatusIconLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry { public StatusTag tag; public Sprite sprite; }

    public List<Entry> entries = new();

    private Dictionary<StatusTag, Sprite> map;
    void OnEnable()
    {
        map = new Dictionary<StatusTag, Sprite>();
        foreach (var e in entries) map[e.tag] = e.sprite;
    }

    void OnValidate()
    {
        // Keep entries in lockstep with the StatusTag enum without scrambling existing assignments
        var existing = new Dictionary<StatusTag, Sprite>();
        if (entries != null)
        {
            foreach (var e in entries) existing[e.tag] = e.sprite; // last wins on duplicates
        }
        var tags = (StatusTag[])System.Enum.GetValues(typeof(StatusTag));
        var rebuilt = new List<Entry>(tags.Length);
        foreach (var tag in tags)
        {
            existing.TryGetValue(tag, out var sprite);
            rebuilt.Add(new Entry { tag = tag, sprite = sprite });
        }
        entries = rebuilt;
        // Rebuild runtime map too for play-mode changes in editor
        OnEnable();
    }

    public Sprite Get(StatusTag tag)
    {
        if (map == null) OnEnable();
        return map != null && map.TryGetValue(tag, out var s) ? s : null;
    }
}


