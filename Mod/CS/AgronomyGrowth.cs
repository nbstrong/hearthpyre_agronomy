using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace HearthpyreAgronomy
{
	[Serializable]
	public sealed class AgronomyGrowth : IPart
	{
		public long GrowthTurns = 1L;
		public long ReadyTurn = long.MaxValue;

		public void Schedule()
		{
			ReadyTurn = The.Game.Turns + Math.Max(1L, GrowthTurns);
		}

		private void CheckReady(long currentTurn)
		{
			if (ReadyTurn == long.MaxValue || currentTurn < ReadyTurn)
				return;

			if (!ParentObject.TryGetPart(out Harvestable harvestable))
			{
				ReadyTurn = long.MaxValue;
				return;
			}

			if (!harvestable.Ripe)
				harvestable.UpdateRipeStatus(newRipeStatus: true);

			ReadyTurn = long.MaxValue;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == ZoneActivatedEvent.ID
				|| ID == ZoneThawedEvent.ID;
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			CheckReady(The.Game.Turns);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			CheckReady(The.Game.Turns);
			return base.HandleEvent(E);
		}

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			CheckReady(TimeTick);
		}

		public override bool AllowStaticRegistration()
		{
			return true;
		}
	}
}
