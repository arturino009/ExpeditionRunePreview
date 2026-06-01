using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExileCore2;
using Newtonsoft.Json;

namespace ExpeditionRunePreview;

public class Recipe
{
    [JsonProperty("reward")] public string Reward { get; set; } = "";
    [JsonProperty("runes")] public List<string> Runes { get; set; } = [];
}

public class RecipeFile
{
    [JsonProperty("aliases")] public Dictionary<string, string> Aliases { get; set; } = new();
    [JsonProperty("runeNames")] public Dictionary<string, string> RuneNames { get; set; } = new();
    [JsonProperty("recipes")] public List<Recipe> Recipes { get; set; } = [];
}

/// <summary>
/// Loads recipes.json and exposes lookups for mapping rune .pet preloads to the recipes they
/// participate in.
/// </summary>
public class RuneData
{
    private Dictionary<string, string> _aliases = new();
    private Dictionary<string, string> _runeNames = new();
    private List<Recipe> _recipes = [];

    // runeKey (e.g. "RemnantRuneTempest") -> recipes that use it.
    private Dictionary<string, List<Recipe>> _recipesByRune = new();

    // set of all rune keys that actually appear in the data (recipes or runeNames).
    private HashSet<string> _knownRunes = new();

    public bool Loaded { get; private set; }
    public int RecipeCount => _recipes.Count;

    public bool Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                DebugWindow.LogError($"{nameof(ExpeditionRunePreview)} -> recipes.json not found at {path}");
                Loaded = false;
                return false;
            }

            var file = JsonConvert.DeserializeObject<RecipeFile>(File.ReadAllText(path)) ?? new RecipeFile();
            _aliases = new Dictionary<string, string>(file.Aliases, StringComparer.OrdinalIgnoreCase);
            _runeNames = file.RuneNames ?? new Dictionary<string, string>();
            _recipes = file.Recipes ?? [];

            _recipesByRune = new Dictionary<string, List<Recipe>>();
            _knownRunes = new HashSet<string>(_runeNames.Keys);
            foreach (var recipe in _recipes)
            {
                // Use distinct so a recipe that needs two of the same rune is only listed once per rune.
                foreach (var rune in recipe.Runes.Distinct())
                {
                    _knownRunes.Add(rune);
                    if (!_recipesByRune.TryGetValue(rune, out var list))
                        _recipesByRune[rune] = list = [];
                    list.Add(recipe);
                }
            }

            Loaded = true;
            return true;
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(ExpeditionRunePreview)} -> failed to load recipes.json: {e}");
            Loaded = false;
            return false;
        }
    }

    /// <summary>
    /// Resolve a rune .pet filename (without extension, e.g. "Electrocuting") to a canonical rune
    /// key, or null if it is unknown. Tries an explicit alias first, then the "RemnantRune" and
    /// "RemnantRareRune" prefix conventions.
    /// </summary>
    public string Resolve(string petBasename)
    {
        if (string.IsNullOrWhiteSpace(petBasename))
            return null;

        if (_aliases.TryGetValue(petBasename, out var aliased))
            return _knownRunes.Contains(aliased) ? aliased : null;

        var normal = "RemnantRune" + petBasename;
        if (_knownRunes.Contains(normal))
            return normal;

        var rare = "RemnantRareRune" + petBasename;
        if (_knownRunes.Contains(rare))
            return rare;

        // Rare-rune .pet files are named "Rare<X>" (e.g. RareWard) -> RemnantRareRune<X>.
        if (petBasename.StartsWith("Rare", StringComparison.OrdinalIgnoreCase) && petBasename.Length > 4)
        {
            var rarePrefixed = "RemnantRareRune" + petBasename[4..];
            if (_knownRunes.Contains(rarePrefixed))
                return rarePrefixed;
        }

        return null;
    }

    public static bool IsRare(string runeKey)
    {
        return runeKey != null && runeKey.StartsWith("RemnantRareRune", StringComparison.Ordinal);
    }

    public string DisplayName(string runeKey)
    {
        if (runeKey != null && _runeNames.TryGetValue(runeKey, out var name))
            return name;

        // Fallback: strip the prefix from the identifier.
        if (runeKey != null && runeKey.StartsWith("RemnantRareRune"))
            return runeKey["RemnantRareRune".Length..] + " Rune";
        if (runeKey != null && runeKey.StartsWith("RemnantRune"))
            return runeKey["RemnantRune".Length..] + " Rune";
        return runeKey ?? "";
    }

    public List<Recipe> RecipesFor(string runeKey)
    {
        return runeKey != null && _recipesByRune.TryGetValue(runeKey, out var list) ? list : [];
    }
}
