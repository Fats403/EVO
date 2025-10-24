using UnityEngine;

public enum SlotOwner { Player1, Player2 }

public class BoardSlot : MonoBehaviour
{
	public bool occupied;
	public Creature currentCreature;
	public SlotOwner owner = SlotOwner.Player1;
	public bool hasPending;
	public CardData pendingCard;

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
	}
}
