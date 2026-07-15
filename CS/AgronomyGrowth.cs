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
			ReconcileGrowthState();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			ReconcileGrowthState();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			ReconcileGrowthState();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			ReconcileGrowthState();
			return base.HandleEvent(E);
		}

		public override bool WantTurnTick() => true;

		public override void TurnTick(long TimeTick, int Amount)
		{
			CheckGrowthState(TimeTick);
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

		public void ReconcileGrowthState(long? currentTime = null)
		{
			if (GrowthTurns <= 0) return;
			if (!ParentObject.TryGetPart(out Harvestable harvestable)) return;

			var game = The.Game;
			if (game == null && !currentTime.HasValue) return;

			ApplyInvariantGrowthState(harvestable);
			CheckGrowthState(currentTime ?? game.TimeTicks, harvestable);
		}

		public void CheckGrowthState(long currentTime)
		{
			if (GrowthTurns <= 0) return;
			if (!ParentObject.TryGetPart(out Harvestable harvestable)) return;

			CheckGrowthState(currentTime, harvestable);
		}

		private void ApplyInvariantGrowthState(Harvestable harvestable)
		{
			harvestable.DestroyOnHarvest = false;
			harvestable.RegenTime = string.Empty;
			harvestable.RegenTimer = int.MaxValue;
		}

		private void CheckGrowthState(long currentTime, Harvestable harvestable)
		{
			if (!harvestable.Ripe && currentTime >= ReadyTurn)
			{
				harvestable.UpdateRipeStatus(true);
			}
		}
	}
}
