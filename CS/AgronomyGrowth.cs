using System;
using System.Collections.Generic;
using XRL.World;

namespace XRL.World.Parts
{
	[Serializable]
	public sealed class AgronomyGrowth : IPart
	{
		public long GrowthTurns;
		public long ReadyTurn;

		public override bool AllowStaticRegistration() => true;

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			base.Write(Basis, Writer);
			var fields = new Dictionary<string, object>
			{
				["GrowthTurns"] = GrowthTurns,
				["ReadyTurn"] = ReadyTurn
			};
			Writer.Write(fields);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			base.Read(Basis, Reader);
			var fields = Reader.ReadDictionary<string, object>();
			object val;
			if (fields.TryGetValue("GrowthTurns", out val)) GrowthTurns = Convert.ToInt64(val);
			if (fields.TryGetValue("ReadyTurn", out val)) ReadyTurn = Convert.ToInt64(val);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
			       || ID == ObjectCreatedEvent.ID
			       || ID == AfterGameLoadedEvent.ID
			       || ID == ZoneActivatedEvent.ID
			       || ID == ZoneThawedEvent.ID
				;
		}

		public override bool HandleEvent(ObjectCreatedEvent E)
		{
			SyncGrowthState();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			SyncGrowthState();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			SyncGrowthState();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			SyncGrowthState();
			return base.HandleEvent(E);
		}

		public override bool WantTurnTick() => true;

		public override void TurnTick(long TimeTick, int Amount)
		{
			SyncGrowthState(TimeTick);
		}

		public void Configure(long growthTurns, long readyTurn)
		{
			GrowthTurns = growthTurns;
			ReadyTurn = readyTurn;
		}

		public void ScheduleFromCurrentTime(long currentTime)
		{
			if (GrowthTurns <= 0) return;
			ReadyTurn = currentTime + GrowthTurns;
		}

		public void SyncGrowthState(long? currentTime = null)
		{
			if (GrowthTurns <= 0) return;
			if (!ParentObject.TryGetPart(out Harvestable harvestable)) return;

			var game = The.Game;
			if (game == null && !currentTime.HasValue) return;

			var time = currentTime ?? game.TimeTicks;
			harvestable.DestroyOnHarvest = false;
			harvestable.RegenTime = string.Empty;
			harvestable.RegenTimer = int.MaxValue;

			if (!harvestable.Ripe && time >= ReadyTurn)
			{
				harvestable.UpdateRipeStatus(true);
				harvestable.RegenTime = string.Empty;
				harvestable.RegenTimer = int.MaxValue;
			}
		}
	}
}
