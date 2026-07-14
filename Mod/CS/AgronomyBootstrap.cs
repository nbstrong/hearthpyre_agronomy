using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Hearthpyre;
using SimpleJSON;
using XRL;
using XRL.Language;
using XRL.Rules;
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

			if (!AgronomyCatalog.Load()) return;
			if (!CanPatchAll()) return;
			if (!PatchAll()) return;
			Initialized = true;

			if (!PatchDescription())
			{
				MetricsManager.LogError("HearthpyreAgronomy initialized without the tooltip description patch.");
			}
		}

		private static bool CanPatchAll()
		{
			if (AccessTools.Method(typeof(HearthpyreBlueprint), nameof(HearthpyreBlueprint.Build), new[] { typeof(GameObject), typeof(bool) }) == null)
			{
				MetricsManager.LogError("HearthpyreAgronomy could not patch HearthpyreBlueprint.Build; the signature may have changed.");
				return false;
			}

			if (AccessTools.Method(typeof(Harvestable), nameof(Harvestable.UpdateRipeStatus), new[] { typeof(bool) }) == null)
			{
				MetricsManager.LogError("HearthpyreAgronomy could not patch Harvestable.UpdateRipeStatus(bool); the signature may have changed.");
				return false;
			}

			return true;
		}

		private static bool PatchAll()
		{
			if (!PatchBuild())
			{
				return false;
			}

			if (!PatchGrowth())
			{
				MetricsManager.LogError("HearthpyreAgronomy could not patch Harvestable.UpdateRipeStatus(bool); the signature may have changed.");
				UnpatchBuild();
				return false;
			}

			return true;
		}

		private static bool PatchBuild()
		{
			var target = AccessTools.Method(typeof(HearthpyreBlueprint), nameof(HearthpyreBlueprint.Build), new[] { typeof(GameObject), typeof(bool) });
			if (Harmony.GetPatchInfo(target)?.Owners?.Contains("HearthpyreAgronomy") == true) return true;

			try
			{
				Harmony.Patch(target, prefix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.BuildPrefix)));
				return true;
			}
			catch (Exception e)
			{
				MetricsManager.LogError("HearthpyreAgronomy failed to patch HearthpyreBlueprint.Build", e);
				return false;
			}
		}

		private static bool PatchGrowth()
		{
			var target = AccessTools.Method(typeof(Harvestable), nameof(Harvestable.UpdateRipeStatus), new[] { typeof(bool) });
			if (Harmony.GetPatchInfo(target)?.Owners?.Contains("HearthpyreAgronomy") == true) return true;

			try
			{
				Harmony.Patch(target, postfix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.UpdateRipeStatusPostfix)));
				return true;
			}
			catch (Exception e)
			{
				MetricsManager.LogError("HearthpyreAgronomy failed to patch Harvestable.UpdateRipeStatus(bool)", e);
				return false;
			}
		}

		private static bool PatchDescription()
		{
			var target = AccessTools.Method(typeof(HearthpyreBlueprint), "HandleEvent", new[] { typeof(GetShortDescriptionEvent) });
			if (target == null)
			{
				return false;
			}

			if (Harmony.GetPatchInfo(target)?.Owners?.Contains("HearthpyreAgronomy") == true) return true;

			try
			{
				Harmony.Patch(target, postfix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.ShortDescriptionPostfix)));
				return true;
			}
			catch (Exception e)
			{
				MetricsManager.LogError("HearthpyreAgronomy failed to patch HearthpyreBlueprint.HandleEvent(GetShortDescriptionEvent)", e);
				return false;
			}
		}

		private static void UnpatchBuild()
		{
			var target = AccessTools.Method(typeof(HearthpyreBlueprint), nameof(HearthpyreBlueprint.Build), new[] { typeof(GameObject), typeof(bool) });
			if (target == null) return;
			Harmony.Unpatch(target, HarmonyPatchType.Prefix, Harmony.Id);
		}

		private static void UnpatchGrowth()
		{
			var target = AccessTools.Method(typeof(Harvestable), nameof(Harvestable.UpdateRipeStatus), new[] { typeof(bool) });
			if (target == null) return;
			Harmony.Unpatch(target, HarmonyPatchType.Postfix, Harmony.Id);
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
		public sealed class Entry
		{
			public string Name;
			public string Blueprint;
			public string HarvestInto;
			public double HValue;
			public bool Valid;
			public string ValidationError;

			public int GrowthDays => Math.Max(1, (int)Math.Ceiling(HValue));
			public long GrowthTurns => (long)GrowthDays * Calendar.TurnsPerDay;
		}

		private static readonly Dictionary<string, Entry> ByBlueprint = new Dictionary<string, Entry>(StringComparer.Ordinal);
		private static readonly Dictionary<string, Entry> DeclaredBlueprints = new Dictionary<string, Entry>(StringComparer.Ordinal);
		private static readonly Dictionary<string, string> NormalizedBlueprints = new Dictionary<string, string>(StringComparer.Ordinal);
		private static bool Loaded;

		private static string Normalize(string value)
		{
			if (string.IsNullOrEmpty(value)) return value;

			var chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
			return new string(chars);
		}

		public static bool Load()
		{
			if (Loaded) return true;

			if (GameObjectFactory.Factory == null)
			{
				MetricsManager.LogError("HearthpyreAgronomy could not validate its catalog because GameObjectFactory.Factory is unavailable.");
				return false;
			}

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
						if (string.IsNullOrEmpty(blueprint))
						{
							MetricsManager.LogError("HearthpyreAgronomy skipped an invalid Agronomy entry in " + file + " because it is missing Value.");
							continue;
						}

						var entry = new Entry
						{
							Name = node["Name"]?.Value ?? blueprint,
							Blueprint = blueprint,
							HarvestInto = node["HarvestInto"]?.Value,
							HValue = node["HValue"]?.AsDouble ?? double.NaN
						};

						if (DeclaredBlueprints.ContainsKey(entry.Blueprint))
						{
							MetricsManager.LogError("HearthpyreAgronomy skipped duplicate Agronomy blueprint " + entry.Blueprint + " from " + file + ".");
							continue;
						}

						DeclaredBlueprints[entry.Blueprint] = entry;
						if (!ValidateEntry(file, entry))
						{
							continue;
						}

						ByBlueprint[entry.Blueprint] = entry;

						var normalized = Normalize(entry.Blueprint);
						if (!string.IsNullOrEmpty(normalized))
						{
							if (NormalizedBlueprints.TryGetValue(normalized, out var existing) && !string.Equals(existing, entry.Blueprint, StringComparison.Ordinal))
							{
								MetricsManager.LogError("HearthpyreAgronomy detected a normalized blueprint collision between " + existing + " and " + entry.Blueprint + " in " + file + ". The normalized alias will keep pointing at " + existing + ".");
							}
							else
							{
								NormalizedBlueprints[normalized] = entry.Blueprint;
							}
						}
					}
				}
				catch (Exception e)
				{
					MetricsManager.LogError("HearthpyreAgronomy failed to load Hearthpyre.json", e);
				}
			});

			Loaded = true;
			return true;
		}

		public static bool TryGet(string blueprint, out Entry entry)
		{
			if (!string.IsNullOrEmpty(blueprint) && ByBlueprint.TryGetValue(blueprint, out entry))
				return true;

			if (!string.IsNullOrEmpty(blueprint) && NormalizedBlueprints.TryGetValue(Normalize(blueprint), out var exact) && ByBlueprint.TryGetValue(exact, out entry))
				return true;

			entry = null;
			return false;
		}

		public static bool TryGetDeclared(string blueprint, out Entry entry)
		{
			if (!string.IsNullOrEmpty(blueprint) && DeclaredBlueprints.TryGetValue(blueprint, out entry))
				return true;

			entry = null;
			return false;
		}

		public static bool IsAgronomy(string blueprint) => TryGet(blueprint, out _);

		private static bool ValidateEntry(string file, Entry entry)
		{
			if (!GameObjectFactory.Factory.Blueprints.TryGetValue(entry.Blueprint, out var plantBlueprint))
			{
				entry.Valid = false;
				entry.ValidationError = "the plant blueprint was not found";
				MetricsManager.LogError("HearthpyreAgronomy skipped " + entry.Blueprint + " from " + file + " because the plant blueprint was not found.");
				return false;
			}

			if (string.IsNullOrEmpty(entry.HarvestInto))
			{
				entry.Valid = false;
				entry.ValidationError = "the HarvestInto field was missing";
				MetricsManager.LogError("HearthpyreAgronomy skipped " + entry.Blueprint + " from " + file + " because the HarvestInto field was missing.");
				return false;
			}

			if (double.IsNaN(entry.HValue) || double.IsInfinity(entry.HValue) || entry.HValue <= 0)
			{
				entry.Valid = false;
				entry.ValidationError = "the HValue was missing or invalid";
				MetricsManager.LogError("HearthpyreAgronomy skipped " + entry.Blueprint + " from " + file + " because HValue is missing or invalid.");
				return false;
			}

			if (!GameObjectFactory.Factory.Blueprints.ContainsKey(entry.HarvestInto))
			{
				entry.Valid = false;
				entry.ValidationError = "the HarvestInto blueprint " + entry.HarvestInto + " was not found";
				MetricsManager.LogError("HearthpyreAgronomy skipped " + entry.Blueprint + " from " + file + " because the HarvestInto blueprint " + entry.HarvestInto + " was not found.");
				return false;
			}

			if (plantBlueprint.GetPart("Harvestable") == null)
			{
				entry.Valid = false;
				entry.ValidationError = "the target blueprint does not have a Harvestable part";
				MetricsManager.LogError("HearthpyreAgronomy skipped " + entry.Blueprint + " from " + file + " because the target blueprint does not have a Harvestable part.");
				return false;
			}

			entry.Valid = true;
			entry.ValidationError = null;
			return true;
		}
	}

	public static class AgronomyPatches
	{
		private static string GetBlueprintName(HearthpyreBlueprint blueprint)
		{
			return blueprint?.Blueprint ?? blueprint?.ParentObject?.Blueprint;
		}

		public static bool BuildPrefix(HearthpyreBlueprint __instance, GameObject Actor, bool Silent, ref bool __result)
		{
			var blueprint = GetBlueprintName(__instance);
			if (!AgronomyCatalog.TryGetDeclared(blueprint, out var declared))
				return true;

			if (!declared.Valid)
			{
				if (!Silent && Actor != null)
				{
					Actor.Failure("That Agronomy entry is invalid: " + declared.ValidationError);
				}

				__result = false;
				return false;
			}

			__result = BuildAgronomyPlant(__instance, Actor, Silent, declared);
			return false;
		}

		public static void ShortDescriptionPostfix(HearthpyreBlueprint __instance, GetShortDescriptionEvent E)
		{
			if (!AgronomyCatalog.TryGet(GetBlueprintName(__instance), out var entry))
				return;

			E.Postfix.Append("\n{{g|Requires }}").Append(entry.HarvestInto).Append("{{g| to build.}}");
			E.Postfix.Append("\n{{g|Consumes the normal xyloschemer charge cost.}}");
			E.Postfix.Append("\n{{g|Grows in }}").Append(entry.GrowthDays).Append(entry.GrowthDays == 1 ? " day." : " days.");
		}

		public static void UpdateRipeStatusPostfix(Harvestable __instance, bool newRipeStatus)
		{
			if (newRipeStatus) return;
			if (__instance?.ParentObject == null) return;
			if (!__instance.ParentObject.TryGetPart(out AgronomyGrowth growth)) return;

			growth.ScheduleFromCurrentTurn(The.Game?.Turns ?? 0L);
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

			if (!cell.IsClear())
			{
				if (!silent)
				{
					actor.Failure("There isn't enough room to build that.");
				}

				return false;
			}

			var created = GameObjectFactory.Factory.CreateObject(entry.Blueprint, blueprint.Brand);
			if (created == null || !string.Equals(created.Blueprint, entry.Blueprint, StringComparison.Ordinal))
			{
				created?.Destroy();
				return false;
			}

			var required = FindRequiredItem(actor, entry.HarvestInto);
			if (required == null)
			{
				created.Destroy();
				if (!silent)
				{
					actor.Failure("You need " + Grammar.A(entry.HarvestInto) + " to build that.");
				}

				return false;
			}

			var part = xyloschemer.GetPart<HearthpyreXyloschemer>();
			if (part == null || !part.UseCharge(ChargeUse: blueprint.Cost, Silent: silent))
			{
				created.Destroy();
				return false;
			}

			HoloZap(cell);
			cell.RemoveObject(blueprint.ParentObject);
			var obj = cell.Construct(created);

			required = required.SplitFromStack();
			required.Destroy(Silent: true);

			blueprint.ParentObject.Destroy();
			ApplyGrowth(obj, entry);

			if (!silent)
			{
				actor.Physics.DidXToYWithZ(
					"zap",
					obj,
					"into existence with",
					xyloschemer,
					IndefiniteDirectObject: true,
					IndirectObjectPossessedBy: actor
				);
			}

			Lattice.Invalidate(cell);
			return true;
		}

		private static void ApplyGrowth(GameObject obj, AgronomyCatalog.Entry entry)
		{
			if (!obj.TryGetPart(out Harvestable harvestable)) return;

			harvestable.DestroyOnHarvest = false;
			harvestable.RegenTime = string.Empty;
			harvestable.RegenTimer = int.MaxValue;

			var growth = obj.IncludePart<AgronomyGrowth>();
			growth.Configure(entry.GrowthTurns, The.Game?.Turns ?? 0L);
			harvestable.UpdateRipeStatus(false);
			growth.ScheduleFromCurrentTurn(The.Game?.Turns ?? 0L);
			growth.SyncGrowthState();
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
