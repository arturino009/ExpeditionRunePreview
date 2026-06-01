using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;

namespace ExpeditionRunePreview;

public class ExpeditionRunePreview : BaseSettingsPlugin<ExpeditionRuneSettings>
{
    // The remnant entity is a server-tracked IngameIcon present zone-wide from area load; its
    // StateMachine "sockets" state is the number of runes the encounter has.
    private const string EncounterPath = "Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter";

    // Moon is the one rune with no unique fx preload, so a remnant with no detected rune is assumed Moon.
    private const string MoonRuneKey = "RemnantRuneMoon";

    // Splits "Exalted Orb x2" into the base name and a stack quantity.
    private static readonly Regex RewardQtyRegex = new(@"^(.*?)\s+x(\d+)$", RegexOptions.Compiled);

    public static ExpeditionRunePreview Plugin;
    public readonly object Locker = new();

    private readonly RuneData _runeData = new();
    private RuneScanner _scanner;
    private RuneRenderer _renderer;

    // Reward values (in Exalted Orbs) read straight from NinjaPricer's on-disk poe.ninja cache.
    private readonly PriceProvider _prices = new();

    // Prebuilt Moon rune entry, shown when a remnant is present but no rune was detected (opt-in).
    private DetectedRune _moonRune;
    public DetectedRune MoonRune => _moonRune;

    public List<DetectedRune> Detected = [];

    // Socket count of every expedition remnant currently in the zone (one entry per remnant).
    public List<int> EncounterSocketCounts = [];

    public bool CanRender;
    public bool IsLoading;

    public bool Working => _scanner.Working;

    private string RecipeDataPath => Path.Combine(DirectoryFullName, "Data", "recipes.json");

    public override bool Initialise()
    {
        Plugin = this;
        LoadRuneData();

        _scanner = new RuneScanner(
            _runeData,
            result =>
            {
                Detected = result;
                IsLoading = false;
            },
            () => Settings.ShowUnknownRunes.Value,
            Locker);
        _renderer = new RuneRenderer();
        RebuildMoonRune();

        GameController.LeftPanel.WantUse(() => Settings.Enable);

        Input.RegisterKey(Settings.RefreshPreloads.Value);
        Settings.RefreshPreloads.OnValueChanged += () => Input.RegisterKey(Settings.RefreshPreloads.Value);

        AreaChange(GameController.Area.CurrentArea);
        return true;
    }

    private void LoadRuneData()
    {
        if (_runeData.Load(RecipeDataPath))
            LogMessage($"{nameof(ExpeditionRunePreview)}: loaded {_runeData.RecipeCount} rune recipes.");
    }

    private void RebuildMoonRune()
    {
        _moonRune = _scanner?.BuildRune(MoonRuneKey, inferred: true);
    }

    public override void AreaChange(AreaInstance area)
    {
        // Scan the new area's preloads exactly once per zone change.
        lock (Locker)
        {
            Detected = [];
        }

        if (GameController.Area.CurrentArea.IsHideout && !Settings.ShowInHideout)
        {
            IsLoading = false;
            return;
        }

        IsLoading = true;
        _scanner.Parse();

        // Refresh prices each zone so we pick up any NinjaPricer re-download between areas.
        ReloadPrices();
    }

    // Read NinjaPricer's cached poe.ninja data (a sibling plugin folder) on a background thread.
    private void ReloadPrices()
    {
        var pluginsDir = Path.GetDirectoryName(DirectoryFullName);
        if (pluginsDir == null)
            return;

        var dataDir = Path.Combine(pluginsDir, "NinjaPricer", "poescoutdata");
        var league = GameController.IngameState.Data.ServerData.League;
        Task.Run(() => _prices.Load(dataDir, league));
    }

    public override void Tick()
    {
        CanRender = true;

        if (!Settings.Enable || GameController.Area.CurrentArea is { IsTown: true } || GameController.IsLoading || !GameController.InGame)
        {
            CanRender = false;
            return;
        }

        if (GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal)
        {
            CanRender = false;
            return;
        }

        UpdateSocketCounts();

        var uiHover = GameController.Game.IngameState.UIHover;
        var miniMap = GameController.Game.IngameState.IngameUi.Map.SmallMiniMap;

        if (uiHover is { Tooltip: not null, IsValid: true } &&
            uiHover.Address != 0x00 &&
            uiHover.Tooltip.Address != 0x00 &&
            uiHover.Tooltip.IsVisibleLocal &&
            uiHover.Tooltip.GetClientRectCache.Intersects(miniMap.GetClientRectCache))
        {
            CanRender = false;
            return;
        }

        if (Settings.RefreshPreloads.PressedOnce())
        {
            AreaChange(GameController.Area.CurrentArea);
            LogMessage("Reloaded expedition rune preloads.");
        }
    }

    // Read the live socket count from each remnant in the zone. Cheap; runs every tick so it picks
    // up the count as soon as the entity is valid (it's available from anywhere in the area).
    private void UpdateSocketCounts()
    {
        if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.IngameIcon, out var icons))
        {
            EncounterSocketCounts = [];
            return;
        }

        EncounterSocketCounts = icons
            .Where(e => e.Path == EncounterPath)
            .Select(GetSocketCount)
            .Where(c => c > 0)
            .ToList();
    }

    private static int GetSocketCount(Entity entity)
    {
        var states = entity.GetComponent<StateMachine>()?.States;
        if (states == null)
            return 0;

        foreach (var state in states)
            if (state.Name == "sockets")
                return (int)state.Value;

        return 0;
    }

    /// <summary>
    /// Total value of a reward string (e.g. "Exalted Orb x2") in Exalted Orbs, matched by name against
    /// NinjaPricer's cached poe.ninja data. Returns 0 if prices aren't loaded or the reward isn't
    /// priceable (sagas, fluxes, keys…).
    /// </summary>
    public double GetRewardValue(string reward)
    {
        if (string.IsNullOrEmpty(reward))
            return 0;

        var baseName = reward;
        var quantity = 1;
        var match = RewardQtyRegex.Match(reward);
        if (match.Success)
        {
            baseName = match.Groups[1].Value;
            quantity = int.Parse(match.Groups[2].Value);
        }

        return _prices.GetValue(baseName) * quantity;
    }

    public override void Render()
    {
        List<DetectedRune> snapshot;
        lock (Locker)
        {
            snapshot = Detected;
        }

        _renderer.Render(IsLoading, snapshot);
    }

    public override void DrawSettings()
    {
        base.DrawSettings();

        if (ImGui.Button("Reload rune data"))
        {
            LoadRuneData();
            RebuildMoonRune();
            AreaChange(GameController.Area.CurrentArea);
        }

        ImGui.SameLine();

        if (ImGui.Button("Re-scan preloads"))
        {
            AreaChange(GameController.Area.CurrentArea);
        }
    }
}
