using UnityEngine;

public enum SlotOwner
{
    Player1,
    Player2,
}

public class BoardSlot : MonoBehaviour
{
    [Header("Identification")]
    public int index = -1; // 0-4 for P1, 5-9 for P2 (or auto-assigned)

    public bool occupied;
    public Creature currentCreature;
    public SlotOwner owner = SlotOwner.Player1;
    public GameObject hoverVisual;

    public Vector2 ScreenPosition => Camera.main.WorldToScreenPoint(transform.position);

    public void Occupy(Creature c)
    {
        currentCreature = c;
        occupied = true;
    }

    public void Vacate()
    {
        currentCreature = null;
        occupied = false;
    }

    // Hover indicator (does not change state)
    public void ShowHoverIndicator(GameObject prefab)
    {
        if (prefab == null || hoverVisual != null)
            return;
        hoverVisual = UnityEngine.Object.Instantiate(prefab, transform);
        hoverVisual.transform.localPosition = new Vector3(0, -32, 0);
    }

    public void HideHoverIndicator()
    {
        if (hoverVisual != null)
        {
            UnityEngine.Object.Destroy(hoverVisual);
            hoverVisual = null;
        }
    }
}
