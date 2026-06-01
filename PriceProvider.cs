using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExileCore2;
using Newtonsoft.Json.Linq;

namespace ExpeditionRunePreview;

/// <summary>
/// Resolves item values (in Exalted Orbs) by name from NinjaPricer's on-disk poe.ninja cache.
/// Fully decoupled from the NinjaPricer plugin: it only reads the JSON files NinjaPricer downloads,
/// so it needs no plugin bridge and no changes to NinjaPricer itself.
/// </summary>
public class PriceProvider
{
    // Exchange overviews that hold the kinds of stackable items expedition rewards can be, all priced
    // in the primary currency (Exalted Orbs on poe2 poe.ninja).
    private static readonly string[] Categories =
    [
        "Currency", "Fragments", "Expedition", "Runes", "Essences",
        "Breach", "Delirium", "UncutGems", "Abyss", "Ritual", "Verisium",
    ];

    private Dictionary<string, double> _values = new(StringComparer.OrdinalIgnoreCase);

    public bool Loaded { get; private set; }

    /// <summary>Rebuild the name -> Exalted-value map from NinjaPricer's cache. Safe to run off-thread.</summary>
    public void Load(string poescoutDataDir, string preferredLeague)
    {
        try
        {
            var leagueDir = ResolveLeagueDir(poescoutDataDir, preferredLeague);
            if (leagueDir == null)
                return;

            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var category in Categories)
            {
                var path = Path.Combine(leagueDir, category + ".json");
                if (!File.Exists(path))
                    continue;

                try
                {
                    LoadCategory(File.ReadAllText(path), values);
                }
                catch
                {
                    // Skip a malformed/partial file rather than failing the whole load.
                }
            }

            // The primary currency is its own unit.
            values.TryAdd("Exalted Orb", 1);

            _values = values;
            Loaded = values.Count > 0;
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(ExpeditionRunePreview)} -> price load failed: {e}");
        }
    }

    /// <summary>Value of a single unit of the named item in Exalted Orbs, or 0 if unknown.</summary>
    public double GetValue(string name)
    {
        return name != null && _values.TryGetValue(name, out var value) ? value : 0;
    }

    // A poe.ninja exchange overview: core.primary / core.rates, items (id -> name), lines (id -> value).
    private static void LoadCategory(string json, Dictionary<string, double> values)
    {
        var root = JObject.Parse(json);
        var core = root["core"];
        var primary = (string)core?["primary"];
        var rate = primary == "exalted" ? 1.0 : (double?)core?["rates"]?["exalted"] ?? 0.0;
        if (rate == 0)
            return;

        var idToName = new Dictionary<string, string>();
        foreach (var item in root["items"] ?? new JArray())
        {
            var id = (string)item["id"];
            var name = (string)item["name"];
            if (id != null && name != null)
                idToName[id] = name;
        }

        foreach (var line in root["lines"] ?? new JArray())
        {
            var id = (string)line["id"];
            if (id == null || !idToName.TryGetValue(id, out var name))
                continue;

            var value = (double?)line["primaryValue"] ?? 0;
            if (value > 0)
                values[name] = value * rate;
        }
    }

    private static string ResolveLeagueDir(string dataDir, string preferredLeague)
    {
        if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
            return null;

        if (!string.IsNullOrEmpty(preferredLeague))
        {
            var preferred = Path.Combine(dataDir, preferredLeague);
            if (Directory.Exists(preferred))
                return preferred;
        }

        // Fall back to the most recently refreshed league folder.
        return new DirectoryInfo(dataDir).GetDirectories()
            .OrderByDescending(d => d.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }
}
