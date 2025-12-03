using UnityEngine;

public class TooltipTrigger : TooltipTriggerBase
{
    [Header("Tooltip Content")]
    public string title;

    [TextArea]
    public string body;
    public Sprite icon;

    protected override void OnShowTooltip(Vector2 screenPosition)
    {
        if (TooltipManager.Instance == null)
            return;

        var data = new TooltipData
        {
            title = title,
            body = body,
            icon = icon,
        };

        TooltipManager.Instance.Show(data, screenPosition, this);
    }

    protected override void OnHideTooltip()
    {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.Hide(this);
    }
}
