using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Utilities for temporarily boosting a creature's render-layer (sorting order)
/// so it appears above neighbors during animations, then restoring it.
/// </summary>
public struct SortingState
{
    public Canvas[] canvases;
    public int[] canvasOrders;
    public bool[] canvasOverrides;

    public SpriteRenderer[] spriteRenderers;
    public int[] spriteOrders;
}

public static class SortingUtils
{
    /// <summary>
    /// Raise all world-space canvases and sprite renderers under the given transform
    /// by sortBoost, returning a snapshot that can be used to restore later.
    /// </summary>
    public static SortingState PushToForeground(Transform root, int sortBoost = 100)
    {
        var state = new SortingState();
        if (root == null)
            return state;

        // World-space canvases
        var canvases = root.GetComponentsInChildren<Canvas>(includeInactive: false);
        state.canvases = canvases;
        if (canvases != null && canvases.Length > 0)
        {
            state.canvasOrders = new int[canvases.Length];
            state.canvasOverrides = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null || c.renderMode != RenderMode.WorldSpace)
                    continue;
                state.canvasOrders[i] = c.sortingOrder;
                state.canvasOverrides[i] = c.overrideSorting;
                c.overrideSorting = true;
                c.sortingOrder = state.canvasOrders[i] + sortBoost;
            }
        }

        // Sprite renderers
        var spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
        state.spriteRenderers = spriteRenderers;
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            state.spriteOrders = new int[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var r = spriteRenderers[i];
                if (r == null)
                    continue;
                state.spriteOrders[i] = r.sortingOrder;
                r.sortingOrder = state.spriteOrders[i] + sortBoost;
            }
        }

        return state;
    }

    /// <summary>
    /// Restore any sorting overrides that were changed by PushToForeground.
    /// Safe to call even if some renderers/canvases have been destroyed.
    /// </summary>
    public static void RestoreSorting(SortingState state)
    {
        if (state.spriteRenderers != null && state.spriteOrders != null)
        {
            int len = Mathf.Min(state.spriteRenderers.Length, state.spriteOrders.Length);
            for (int i = 0; i < len; i++)
            {
                var r = state.spriteRenderers[i];
                if (r != null)
                {
                    r.sortingOrder = state.spriteOrders[i];
                }
            }
        }

        if (state.canvases != null && state.canvasOrders != null && state.canvasOverrides != null)
        {
            int len = Mathf.Min(
                state.canvases.Length,
                Mathf.Min(state.canvasOrders.Length, state.canvasOverrides.Length)
            );
            for (int i = 0; i < len; i++)
            {
                var c = state.canvases[i];
                if (c == null || c.renderMode != RenderMode.WorldSpace)
                    continue;
                c.overrideSorting = state.canvasOverrides[i];
                c.sortingOrder = state.canvasOrders[i];
            }
        }
    }
}
