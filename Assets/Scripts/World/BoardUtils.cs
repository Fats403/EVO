using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BoardUtils
{
	// Returns the BoardSlot that currently contains the creature (or null)
	public static BoardSlot GetSlotOf(Creature c)
	{
		if (c == null) return null;
		var slots = Object.FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);
		foreach (var s in slots)
		{
			if (s != null && s.currentCreature == c) return s;
		}
		return null;
	}

	// Adjacent allies: same owner, immediately left/right by x-order among occupied slots
	public static IEnumerable<Creature> GetAdjacentAllies(Creature c)
	{
		var result = new List<Creature>();
		if (c == null) return result;
		var mySlot = GetSlotOf(c);
		if (mySlot == null) return result;

		var sameOwnerSlots = Object.FindObjectsByType<BoardSlot>(FindObjectsSortMode.None)
			.Where(s => s != null && s.owner == mySlot.owner && s.occupied && s.currentCreature != null)
			.OrderBy(s => s.transform.position.x)
			.ToList();

		int idx = sameOwnerSlots.FindIndex(s => s.currentCreature == c);
		if (idx < 0) return result;

		// left
		if (idx - 1 >= 0)
		{
			var left = sameOwnerSlots[idx - 1].currentCreature;
			if (left != null) result.Add(left);
		}
		// right
		if (idx + 1 < sameOwnerSlots.Count)
		{
			var right = sameOwnerSlots[idx + 1].currentCreature;
			if (right != null) result.Add(right);
		}
		return result;
	}

	// Closest living enemy by world-space distance
	public static Creature GetClosestEnemy(Creature c)
	{
		if (c == null) return null;
		var enemies = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
			.Where(x => x != null && x.currentHealth > 0 && !x.isDying && x.owner != c.owner)
			.ToList();
		if (enemies.Count == 0) return null;
		var pos = c.transform.position;
		return enemies.OrderBy(e => Vector3.SqrMagnitude(e.transform.position - pos)).FirstOrDefault();
	}
}


