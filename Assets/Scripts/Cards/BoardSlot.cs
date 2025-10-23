using UnityEngine;

public enum SlotOwner { Player1, Player2 }

public class BoardSlot : MonoBehaviour
{
	public bool occupied;
	public Creature currentCreature;
	public SlotOwner owner = SlotOwner.Player1;

	public Vector2 ScreenPosition => Camera.main.WorldToScreenPoint(transform.position);

	public void Occupy(Creature c)
	{
		currentCreature = c;
		occupied = true;
	}
}
