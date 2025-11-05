using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Status Icon Library")]
public class StatusIconLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry { public StatusTag tag; public Sprite sprite; }

    public List<Entry> entries = new List<Entry>();

    private Dictionary<StatusTag, Sprite> map;
    void OnEnable()
    {
        map = new Dictionary<StatusTag, Sprite>();
        foreach (var e in entries) map[e.tag] = e.sprite;
    }

    public Sprite Get(StatusTag tag)
    {
        if (map == null) OnEnable();
        return map != null && map.TryGetValue(tag, out var s) ? s : null;
    }
}


