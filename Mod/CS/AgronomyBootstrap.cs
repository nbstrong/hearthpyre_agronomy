using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using SimpleJSON;
using Hearthpyre;
using Hearthpyre.Realm;
using XRL;
using XRL.Language;
using XRL.Core;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;
using static Hearthpyre.Static;

namespace HearthpyreAgronomy
{
	public static class AgronomyBootstrap
	{
		private static readonly Harmony Harmony = new Harmony("HearthpyreAgronomy");
		private static bool Initialized;

		public static void Initialize()
		{
			if (Initialized) return;
			Initialized = true;

			AgronomyCatalog.Load();
			PatchBuild();
			PatchDescription();
			PatchRipeStatus();
		}

		private static void PatchBuild()
		{
			var target = AccessTools.Method(
				typeof(HearthpyreBlueprint),
				nameof(HearthpyreBlueprint.Build),
				new[] { typeof(GameObject), typeof(bool) }
			);
			if (target == null)
			{
				MetricsManager.LogError(
					"HearthpyreAgronomy could not find HearthpyreBlueprint.Build(GameObject, bool)",
					new MissingMethodException(typeof(HearthpyreBlueprint).FullName, nameof(HearthpyreBlueprint.Build))
				);
				return;
			}

			Harmony.Patch(target, prefix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.BuildPrefix)));
		}

		private static void PatchDescription()
		{
			var target = AccessTools.Method(typeof(HearthpyreBlueprint), "HandleEvent", new[] { typeof(GetShortDescriptionEvent) });
			if (target == null)
			{
				MetricsManager.LogError(
					"HearthpyreAgronomy could not find HearthpyreBlueprint.HandleEvent(GetShortDescriptionEvent)",
					new MissingMethodException(typeof(HearthpyreBlueprint).FullName, "HandleEvent")
				);
				return;
			}

			Harmony.Patch(target, postfix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.ShortDescriptionPostfix)));
		}

		private static void PatchRipeStatus()
		{
			var target = AccessTools.Method(typeof(Harvestable), nameof(Harvestable.UpdateRipeStatus), new[] { typeof(bool) });
			if (target == null)
			{
				MetricsManager.LogError(
					"HearthpyreAgronomy could not find Harvestable.UpdateRipeStatus(bool)",
					new MissingMethodException(typeof(Harvestable).FullName, nameof(Harvestable.UpdateRipeStatus))
				);
				return;
			}

			Harmony.Patch(target, postfix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.RipeStatusPostfix)));
		}
	}

	[PlayerMutator]
	public sealed class AgronomyMutator : IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			AgronomyBootstrap.Initialize();
		}
	}

	public static class AgronomyCatalog
	{
		private static readonly Dictionary<string, Entry> ByBlueprint = new Dictionary<string, Entry>(StringComparer.Ordinal);
		private static bool Loaded;

		private static string Normalize(string value)
		{
			if (string.IsNullOrEmpty(value)) return value;

			var chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
			return new string(chars);
		}

		public sealed class Entry
		{
			public string Name;
			public string Blueprint;
			public string HarvestInto;
			public double HValue;

			public int GrowthDays => Math.Max(1, (int)Math.Ceiling(HValue));
		}

		public static void Load()
		{
			if (Loaded) return;
			Loaded = true;

			ModManager.ForEachFile("Hearthpyre.json", (file, mod) =>
			{
				if (!mod.IsApproved) return;

				try
				{
					var data = JSON.Parse(File.ReadAllText(file)) as JSONClass;
					var items = data?["BLUEPRINTS"]?["Agronomy"]?["Items"]?.AsArray;
					if (items == null) return;

					foreach (JSONNode node in items)
					{
						var blueprint = node["Value"]?.Value;
						var harvestInto = node["HarvestInto"]?.Value;
						if (string.IsNullOrEmpty(blueprint) || string.IsNullOrEmpty(harvestInto))
							continue;

						var entry = new Entry
						{
							Name = node["Name"]?.Value ?? blueprint,
							Blueprint = blueprint,
							HarvestInto = harvestInto,
							HValue = node["HValue"]?.AsDouble ?? 1d
						};
						ByBlueprint[entry.Blueprint] = entry;
						ByBlueprint[Normalize(entry.Blueprint)] = entry;
					}
				}
				catch (Exception e)
				{
					MetricsManager.LogError("HearthpyreAgronomy failed to load Hearthpyre.json", e);
				}
			});
		}

		public static bool TryGet(string blueprint, out Entry entry)
		{
			if (!string.IsNullOrEmpty(blueprint) && ByBlueprint.TryGetValue(blueprint, out entry))
				return true;

			if (!string.IsNullOrEmpty(blueprint) && ByBlueprint.TryGetValue(Normalize(blueprint), out entry))
				return true;

			entry = null;
			return false;
		}

		public static bool IsAgronomy(string blueprint) => TryGet(blueprint, out _);
	}

	public static class AgronomyPatches
	{
		private static string GetBlueprintName(HearthpyreBlueprint blueprint)
		{
			return blueprint?.Blueprint ?? blueprint?.ParentObject?.Blueprint;
		}

		public static bool BuildPrefix(HearthpyreBlueprint __instance, GameObject Actor, bool Silent, ref bool __result)
		{
			if (!AgronomyCatalog.TryGet(GetBlueprintName(__instance), out var entry))
				return true;

			__result = BuildAgronomyPlant(__instance, Actor, Silent, entry);
			return false;
		}

		public static void ShortDescriptionPostfix(HearthpyreBlueprint __instance, GetShortDescriptionEvent E)
		{
			if (!AgronomyCatalog.TryGet(GetBlueprintName(__instance), out var entry))
				return;

			E.Postfix.Append("\n{{g|Requires }}").Append(entry.HarvestInto).Append("{{g| to build.}}");
			E.Postfix.Append("\n{{g|Grows in }}").Append(entry.GrowthDays).Append(entry.GrowthDays == 1 ? " day." : " days.");
		}

		public static void RipeStatusPostfix(Harvestable __instance, bool __0)
		{
			if (__0 || __instance?.ParentObject == null)
				return;

			if (__instance.ParentObject.TryGetPart(out AgronomyGrowth growth))
				growth.Schedule();
		}

		private static bool BuildAgronomyPlant(HearthpyreBlueprint blueprint, GameObject actor, bool silent, AgronomyCatalog.Entry entry)
		{
			var cell = blueprint.ParentObject?.CurrentCell;
			if (cell == null || actor == null || actor.CurrentCell == null) return false;
			if (cell == actor.CurrentCell) return false;

			var xyloschemer = actor.GetItemWithBlueprint(OBJ_SCMR);
			if (xyloschemer == null)
			{
				if (!silent)
				{
					actor.Failure(CheckEpistemicStatus(OBJ_SCMR)
						? "You can't build that without a xyloschemer."
						: "You don't have anything to build that with.");
				}

				return false;
			}

			var required = FindRequiredItem(actor, entry.HarvestInto);
			if (required == null)
			{
				if (!silent)
				{
					actor.Failure("You need " + Grammar.A(entry.HarvestInto) + " to build that.");
				}

				return false;
			}

			var part = xyloschemer.GetPart<HearthpyreXyloschemer>();
			if (part == null) return false;
			if (!part.UseCharge(ChargeUse: blueprint.Cost, Silent: silent)) return false;

			HoloZap(cell);
			cell.RemoveObject(blueprint.ParentObject);
			blueprint.ParentObject.Destroy();

			var obj = cell.Construct(GameObjectFactory.Factory.CreateObject(blueprint.Blueprint, blueprint.Brand));
			if (obj == null) return false;

			required = required.SplitFromStack();
			required.Destroy(Silent: true);

			ApplyGrowth(obj, entry.GrowthDays);

			if (!silent)
			{
				actor.Physics.DidXToYWithZ(
					"zap",
					obj,
					"into existance with",
					xyloschemer,
					IndefiniteDirectObject: true,
					IndirectObjectPossessedBy: actor
				);
			}

			Lattice.Invalidate(cell);
			return true;
		}

		private static void ApplyGrowth(GameObject obj, int days)
		{
			if (!obj.TryGetPart(out Harvestable harvestable)) return;

			var growth = new AgronomyGrowth
			{
				GrowthTurns = Math.Max(1L, (long)days * Calendar.TurnsPerDay)
			};

			harvestable.DestroyOnHarvest = false;
			harvestable.RegenTime = "";
			harvestable.RegenTimer = int.MaxValue;
			obj.AddPart(growth);
			harvestable.UpdateRipeStatus(newRipeStatus: false);
			growth.Schedule();
		}

		private static GameObject FindRequiredItem(GameObject actor, string blueprint)
		{
			var normalized = Normalize(blueprint);
			foreach (var item in actor.YieldItems())
			{
				if (item.Blueprint == blueprint) return item;
				if (Normalize(item.Blueprint) == normalized) return item;
			}

			return null;
		}

		private static string Normalize(string value)
		{
			if (string.IsNullOrEmpty(value)) return value;

			var chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
			return new string(chars);
		}
	}
}
