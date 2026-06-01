using System.Drawing;
using System.Windows.Forms;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace ExpeditionRunePreview;

public class ExpeditionRuneSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Show in hideout", "Also scan and display while in your hideout.")]
    public ToggleNode ShowInHideout { get; set; } = new(false);

    [Menu("Refresh hotkey", "Re-scan the current area's preloads.")]
    public HotkeyNodeV2 RefreshPreloads { get; set; } = new(Keys.F5);

    [Menu("Show unknown runes", "Show a line for any rune .pet preload that could not be matched to a known rune (so its alias can be added).")]
    public ToggleNode ShowUnknownRunes { get; set; } = new(true);

    [Menu("Assume Moon rune when none detected", "If a remnant is present but no rune is found in preloads, assume it's the Moon rune (Moon has no unique fx preload). Note: a non-Moon rune that simply hasn't streamed in yet (you're far from the remnant) can be mislabelled until you get closer.")]
    public ToggleNode AssumeMoonRune { get; set; } = new(true);

    [Menu("Filter rewards by socket count", "Only show rewards craftable at this encounter, i.e. whose recipe needs no more runes than the remnant has sockets. Off = show every reward each rune can contribute to.")]
    public ToggleNode FilterBySocketCount { get; set; } = new(true);

    [Menu("Show reward values", "Append each reward's value (in Exalted Orbs) from NinjaPricer, and sort rewards by value. Requires the NinjaPricer plugin loaded with price data.")]
    public ToggleNode ShowRewardValues { get; set; } = new(true);

    [Menu("Max rewards per rune", "Limit how many reward lines are shown under each rune. 0 = show all.")]
    public RangeNode<int> MaxRecipesPerRune { get; set; } = new(0, 0, 60);

    [Menu("Draw X offset", "Horizontal offset of the overlay panel.")]
    public RangeNode<int> DrawXOffset { get; set; } = new(0, -2000, 2000);

    [Menu("Background padding")]
    public RangeNode<int> BackgroundPadding { get; set; } = new(4, 0, 400);

    [Menu("Background rounding")]
    public RangeNode<float> BackgroundRounding { get; set; } = new(0f, 0f, 40f);

    public ColorNode BackgroundColor { get; set; } = new(Color.FromArgb(160, 0, 0, 0));

    [Menu("Encounter header color", "Color of the 'Expedition: N runes' socket-count summary line.")]
    public ColorNode EncounterHeaderColor { get; set; } = new(Color.FromArgb(255, 255, 235, 150));

    [Menu("Rune header color", "Color of normal rune name header lines.")]
    public ColorNode RuneNameColor { get; set; } = new(Color.FromArgb(255, 255, 215, 90));

    [Menu("Rare rune header color", "Color of rare rune name header lines.")]
    public ColorNode RareRuneNameColor { get; set; } = new(Color.FromArgb(255, 120, 220, 255));

    [Menu("Recipe text color", "Color of the per-recipe reward lines.")]
    public ColorNode DefaultTextColor { get; set; } = new(Color.FromArgb(210, 210, 210));

    [Menu("Unknown rune color")]
    public ColorNode UnknownRuneColor { get; set; } = new(Color.FromArgb(255, 255, 120, 120));
}
