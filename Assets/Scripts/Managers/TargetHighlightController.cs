using UnityEngine;

public class TargetHighlightController : MonoBehaviour
{
    [Tooltip("Optional highlight object (outline/glow) toggled on valid targeting")]
    public GameObject highlightObject;

    public void SetHighlighted(bool on)
    {
        if (highlightObject != null) highlightObject.SetActive(on);
    }
}


