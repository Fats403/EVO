using UnityEngine;

public class TooltipTrigger : TooltipTriggerBase
{
    [Header("Tooltip Content")]
    public string title;

    [TextArea]
    public string body;
    public Sprite icon;

    [Header("Optional Status Info")]
    [Tooltip(
        "If true, this tooltip will show a dedicated stacks readout using TooltipManager.stacksText."
    )]
    public bool useStacks;

    [Tooltip("Number of stacks to display when useStacks is true.")]
    public int stacks;

    protected override void OnShowTooltip(Vector2 screenPosition)
    {
        if (TooltipManager.Instance == null)
            return;

        var data = new TooltipData
        {
            title = title,
            body = body,
            icon = icon,
            hasStacks = useStacks && stacks > 1,
            stacks = useStacks ? stacks : 0,
        };

        TooltipManager.Instance.Show(data, screenPosition, this);
    }

    protected override void OnHideTooltip()
    {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.Hide(this);
    }
}
