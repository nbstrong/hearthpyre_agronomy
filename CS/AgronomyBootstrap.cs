using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Hearthpyre;
using Hearthpyre.Realm;
using SimpleJSON;
using ConsoleLib.Console;
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

			AgronomyPatches.RepairCrystallineRadicleIcon();

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

			if (!PatchNotitia())
			{
				MetricsManager.LogError("HearthpyreAgronomy could not patch Notitia.Init; the icon repair fallback may not run on reload.");
			}

			if (!PatchBlueprintPicker())
			{
				MetricsManager.LogError("HearthpyreAgronomy could not patch BlueprintPicker.Enter; the icon repair fallback may not run when the menu opens.");
			}

			return true;
		}

		private static bool PatchBuild()
		{
			var target = AccessTools.Method(typeof(HearthpyreBlueprint), nameof(HearthpyreBlueprint.Build), new[] { typeof(GameObject), typeof(bool) });
			if (Harmony.GetPatchInfo(target)?.Owners?.Contains("HearthpyreAgronomy") == true) return true;

			try
			{
				Harmony.Patch(
					target,
					prefix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.BuildPrefix)),
					postfix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.BuildPostfix))
				);
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

		private static bool PatchNotitia()
		{
			var target = AccessTools.Method(typeof(Notitia), nameof(Notitia.Init), Type.EmptyTypes);
			if (target == null)
			{
				return false;
			}

			if (Harmony.GetPatchInfo(target)?.Owners?.Contains("HearthpyreAgronomy") == true) return true;

			try
			{
				Harmony.Patch(target, postfix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.RepairCrystallineRadicleIconPostfix)));
				return true;
			}
			catch (Exception e)
			{
				MetricsManager.LogError("HearthpyreAgronomy failed to patch Notitia.Init", e);
				return false;
			}
		}

		private static bool PatchBlueprintPicker()
		{
			var target = AccessTools.Method(typeof(Hearthpyre.UI.BlueprintPicker), nameof(Hearthpyre.UI.BlueprintPicker.Enter), Type.EmptyTypes);
			if (target == null)
			{
				return false;
			}

			if (Harmony.GetPatchInfo(target)?.Owners?.Contains("HearthpyreAgronomy") == true) return true;

			try
			{
				Harmony.Patch(target, postfix: new HarmonyMethod(typeof(AgronomyPatches), nameof(AgronomyPatches.RepairCrystallineRadicleIconPostfix)));
				return true;
			}
			catch (Exception e)
			{
				MetricsManager.LogError("HearthpyreAgronomy failed to patch BlueprintPicker.Enter", e);
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

	[HasCallAfterGameLoaded]
	public static class AgronomyLoadBootstrap
	{
		[CallAfterGameLoaded]
		public static void AfterGameLoaded()
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
			public bool AlwaysVisible;
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
#pragma warning disable 0612
				if (!mod.IsApproved) return;
#pragma warning restore 0612

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
							AlwaysVisible = node["AlwaysVisible"]?.AsBool ?? false,
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
		private const string CrystallineRadicleBlueprint = "Crystalline Radicle";
		private const string CrystallineRadicleIconTile = "Tiles2/Roots/base_ew.png";

		private sealed class BuildState
		{
			public AgronomyCatalog.Entry Entry;
			public GameObject Required;
			public Cell Cell;
			public List<GameObject> BeforeObjects;
		}

		public static void RepairCrystallineRadicleIconPostfix()
		{
			RepairCrystallineRadicleIcon();
		}

		public static void RepairCrystallineRadicleIcon()
		{
			if (Notitia.Categories == null)
				return;

			foreach (var category in Notitia.Categories.Values)
			{
				if (category?.AllBlueprints == null)
					continue;

				foreach (var blueprint in category.AllBlueprints)
				{
					if (!string.Equals(blueprint?.Value?.Name, CrystallineRadicleBlueprint, StringComparison.Ordinal))
						continue;

					ApplyCrystallineRadicleIcon(blueprint.Icon);
				}
			}
		}

		private static void ApplyCrystallineRadicleIcon(ConsoleChar icon)
		{
			if (icon == null)
				return;

			icon.Tile = CrystallineRadicleIconTile;
			icon.Foreground = CLD_MAG;
			icon.Background = CLD_BLK;
			icon.TileForeground = CLD_MAG;
			icon.TileBackground = CLD_BLK;
			icon.Detail = CLD_YEL;
		}

		private static string GetBlueprintName(HearthpyreBlueprint blueprint)
		{
			return blueprint?.Blueprint ?? blueprint?.ParentObject?.Blueprint;
		}

		private static bool BuildPrefix(HearthpyreBlueprint __instance, GameObject Actor, bool Silent, ref bool __result, ref BuildState __state)
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

			var cell = __instance?.ParentObject?.CurrentCell;
			if (cell == null || Actor == null || Actor.CurrentCell == null || cell == Actor.CurrentCell)
				return true;

			var required = FindRequiredItem(Actor, declared.HarvestInto);
			if (required == null)
			{
				if (!Silent)
				{
					Actor.Failure("You need " + GetRequiredItemPhrase(declared.HarvestInto) + " to build that.");
				}

				__result = false;
				return false;
			}

			__state = new BuildState
			{
				Entry = declared,
				Required = required,
				Cell = cell,
				BeforeObjects = cell.Objects.ToList()
			};

			return true;
		}

		private static void BuildPostfix(HearthpyreBlueprint __instance, GameObject Actor, bool Silent, ref bool __result, BuildState __state)
		{
			if (!__result || __state == null || __state.Entry == null)
				return;

			var cell = __state.Cell ?? __instance?.ParentObject?.CurrentCell;
			if (cell == null)
				return;

			var before = __state.BeforeObjects ?? new List<GameObject>();
			var obj = cell.Objects.FirstOrDefault(x => !before.Contains(x) && string.Equals(x?.Blueprint, __state.Entry.Blueprint, StringComparison.Ordinal));
			if (obj == null)
			{
				MetricsManager.LogError("HearthpyreAgronomy could not identify the plant created by HearthpyreBlueprint.Build for " + __state.Entry.Blueprint + ".");
				__result = false;
				return;
			}

			var required = __state.Required;
			if (required != null)
			{
				required = required.SplitFromStack();
				required.Destroy(Silent: true);
			}

			ApplyGrowth(obj, __state.Entry);
		}

		public static void ShortDescriptionPostfix(HearthpyreBlueprint __instance, GetShortDescriptionEvent E)
		{
			if (!AgronomyCatalog.TryGet(GetBlueprintName(__instance), out var entry))
				return;

			E.Postfix.Append("\n{{g|Requires }}").Append(GetRequiredItemPhrase(entry.HarvestInto)).Append("{{g| to build.}}");
			E.Postfix.Append("\n{{g|Consumes the normal xyloschemer charge cost.}}");
			E.Postfix.Append("\n{{g|Grows in }}").Append(entry.GrowthDays).Append(entry.GrowthDays == 1 ? " day." : " days.");
		}

		public static void UpdateRipeStatusPostfix(Harvestable __instance, bool newRipeStatus)
		{
			if (newRipeStatus) return;
			if (__instance?.ParentObject == null) return;
			if (!__instance.ParentObject.TryGetPart(out AgronomyGrowth growth)) return;

			growth.ScheduleFromCurrentTime(The.Game?.TimeTicks ?? 0L);
		}

		private static void ApplyGrowth(GameObject obj, AgronomyCatalog.Entry entry)
		{
			ApplyPlayerBuiltVisibility(obj, entry);

			if (!obj.TryGetPart(out Harvestable harvestable)) return;

			harvestable.DestroyOnHarvest = false;
			harvestable.RegenTime = string.Empty;
			harvestable.RegenTimer = int.MaxValue;

			var currentTime = The.Game?.TimeTicks ?? 0L;
			var growth = obj.IncludePart<AgronomyGrowth>();
			growth.Configure(entry.GrowthTurns, currentTime);
			harvestable.UpdateRipeStatus(false);
			growth.ScheduleFromCurrentTime(currentTime);
			growth.SyncGrowthState(currentTime);
		}

		private static void ApplyPlayerBuiltVisibility(GameObject obj, AgronomyCatalog.Entry entry)
		{
			if (!entry.AlwaysVisible)
				return;

			if (obj.TryGetPart(out Hidden hidden))
			{
				hidden.Reveal(Silent: true);
				obj.RemovePart(hidden);
			}

			if (obj.Render != null)
				obj.Render.Visible = true;
		}

		private static string GetRequiredItemPhrase(string blueprint)
		{
			var sample = GameObjectFactory.Factory.CreateSampleObject(blueprint);
			if (sample == null) return Grammar.A(blueprint);

			return sample.a + sample.DisplayNameOnlyDirect;
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
