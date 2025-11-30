using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Status Definition Library")]
public class StatusDefinitionLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public StatusTag tag;
        public string displayName;

        [TextArea]
        public string description;
        public Sprite icon;
    }

    public List<Entry> entries = new();

    private Dictionary<StatusTag, Entry> map;

    private void OnEnable()
    {
        if (entries == null)
            entries = new List<Entry>();

        map = new Dictionary<StatusTag, Entry>();
        foreach (var e in entries)
        {
            map[e.tag] = e;
        }
    }

    public bool TryGet(StatusTag tag, out Entry entry)
    {
        entry = default;

        if (map == null)
            OnEnable();

        if (map == null)
        {
            return false;
        }

        if (map.TryGetValue(tag, out var found))
        {
            entry = found;
            return true;
        }

        entry = default;
        return false;
    }

    public string GetDisplayName(StatusTag tag)
    {
        if (TryGet(tag, out var e) && !string.IsNullOrEmpty(e.displayName))
            return e.displayName;

        return tag.ToString();
    }

    public string GetDescription(StatusTag tag)
    {
        return TryGet(tag, out var e) ? (e.description ?? string.Empty) : string.Empty;
    }

    public Sprite GetIcon(StatusTag tag)
    {
        return TryGet(tag, out var e) ? e.icon : null;
    }
}
