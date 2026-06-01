using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ExileCore2;
using ExileCore2.PoEMemory;

namespace ExpeditionRunePreview;

/// <summary>A single reward a rune can contribute to, and how many runes that recipe needs.</summary>
public readonly record struct RewardOption(string Reward, int RuneCount);

/// <summary>A rune detected in the current area's preloads, with the recipes it can contribute to.</summary>
public class DetectedRune
{
    public string Name { get; init; } = "";
    public List<RewardOption> Rewards { get; init; } = [];
    public bool IsUnknown { get; init; }
    public bool IsRare { get; init; }
}

public class RuneScanner(RuneData runeData, Action<List<DetectedRune>> setResult, Func<bool> showUnknown, object locker)
{
    private static readonly Regex RunePetRegex = new(
        @"Expedition/Objects/ExpeditionRemnant/fx/Runes/(?<name>[^/@]+)\.pet",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // On re-entry to a cached zone the rune preloads aren't re-tagged with the new AreaChangeCount
    // immediately, so a single instant scan can miss them. Retry briefly until they show up.
    private const int MaxAttempts = 15;
    private const int RetryDelayMs = 200;

    private int _generation;

    public bool Working { get; private set; }

    public void Parse()
    {
        // Each call supersedes any in-flight scan; the older one self-aborts via the generation check.
        var myGen = Interlocked.Increment(ref _generation);
        Working = true;
        Task.Run(() => RunScan(myGen));
    }

    private void RunScan(int myGen)
    {
        var result = new List<DetectedRune>();
        try
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (myGen != _generation)
                    return; // a newer zone change started another scan; drop this one

                var found = new SortedDictionary<string, string>(StringComparer.Ordinal); // runeKey -> petName
                var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                var memory = ExpeditionRunePreview.Plugin.GameController.Memory;
                var allFiles = new FilesFromMemory(memory).GetAllFiles();
                var areaChangeCount = ExpeditionRunePreview.Plugin.GameController.Game.AreaChangeCount;

                foreach (var file in allFiles)
                {
                    if (file.Value.ChangeCount != areaChangeCount)
                        continue;

                    var match = RunePetRegex.Match(file.Key);
                    if (!match.Success)
                        continue;

                    var petName = match.Groups["name"].Value;
                    var runeKey = runeData.Resolve(petName);
                    if (runeKey == null)
                        unknown.Add(petName);
                    else
                        found[runeKey] = petName;
                }

                if (found.Count > 0 || unknown.Count > 0)
                {
                    result = BuildResult(found, unknown);
                    break;
                }

                Thread.Sleep(RetryDelayMs);
            }
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(ExpeditionRunePreview)} -> scan failed: {e}");
        }
        finally
        {
            // Only the latest scan publishes, so an aborted scan never clobbers fresh results.
            if (myGen == _generation)
            {
                lock (locker)
                {
                    setResult(result);
                }
                Working = false;
            }
        }
    }

    private List<DetectedRune> BuildResult(SortedDictionary<string, string> found, SortedSet<string> unknown)
    {
        var result = new List<DetectedRune>();

        foreach (var runeKey in found.Keys.OrderBy(runeData.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var isRare = RuneData.IsRare(runeKey);
            var recipes = runeData.RecipesFor(runeKey);

            // Every (reward, rune-count) pair this rune can contribute to, including recipes that also
            // need rare runes: the remnant only inscribes this rune, the player supplies the rest, so a
            // rare-requiring recipe is still achievable. Kept un-collapsed so the renderer can filter by
            // the encounter's socket count; it collapses duplicates itself.
            var rewards = recipes
                .Select(r => new RewardOption(r.Reward, r.Runes.Count))
                .Distinct()
                .OrderBy(x => x.RuneCount)
                .ThenBy(x => x.Reward, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Add(new DetectedRune
            {
                Name = runeData.DisplayName(runeKey),
                Rewards = rewards,
                IsRare = isRare,
            });
        }

        if (showUnknown())
        {
            foreach (var name in unknown)
            {
                result.Add(new DetectedRune
                {
                    Name = name,
                    IsUnknown = true,
                });
            }
        }

        return result;
    }
}
