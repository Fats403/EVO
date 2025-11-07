using UnityEngine;

public class GlobalEffectDropZone : MonoBehaviour
{
    public static GlobalEffectDropZone Instance { get; private set; }
    public RectTransform rect;
    public Canvas canvas;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (rect == null) rect = GetComponent<RectTransform>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
    }

    public bool IsPointerInside(Vector2 screenPos)
    {
        if (rect == null) return false;
        var cam = canvas != null ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }
}


