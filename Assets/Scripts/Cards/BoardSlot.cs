using UnityEngine;

public enum SlotOwner { Player1, Player2 }

public class BoardSlot : MonoBehaviour
{
	public bool occupied;
	public Creature currentCreature;
	public SlotOwner owner = SlotOwner.Player1;
	public bool hasPending;
	public CardData pendingCard;
	public GameObject pendingVisual;
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

	public bool SetPending(CardData data)
	{
		if (occupied || hasPending) return false;
		pendingCard = data;
		hasPending = true;
		return true;
	}

	public void ClearPending()
	{
		hasPending = false;
		pendingCard = null;
		if (pendingVisual != null)
		{
			UnityEngine.Object.Destroy(pendingVisual);
			pendingVisual = null;
		}
	}

	public void ShowPendingVisual(GameObject prefab)
	{
		if (prefab == null || pendingVisual != null) return;
		pendingVisual = UnityEngine.Object.Instantiate(prefab, transform);
		pendingVisual.transform.localPosition = Vector3.zero;
	}

	// Hover indicator (does not change state)
	public void ShowHoverIndicator(GameObject prefab)
	{
		if (prefab == null || hoverVisual != null) return;
		hoverVisual = UnityEngine.Object.Instantiate(prefab, transform);
		hoverVisual.transform.localPosition = Vector3.zero;
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
