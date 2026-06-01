using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using RectangleF = ExileCore2.Shared.RectangleF;

namespace ExpeditionRunePreview;

public class RuneRenderer
{
    // Compact Exalted-Orb value label, precision scaled to magnitude.
    private static string FormatValue(double ex)
    {
        var text = ex >= 100 ? ex.ToString("0")
            : ex >= 10 ? ex.ToString("0.#")
            : ex >= 1 ? ex.ToString("0.0")
            : ex.ToString("0.00");
        return $"{text} ex";
    }

    public void Render(bool isLoading, List<DetectedRune> runes)
    {
        var plugin = ExpeditionRunePreview.Plugin;
        if (!plugin.CanRender) return;

        var settings = plugin.Settings;
        var graphics = plugin.Graphics;

        var textTopRight = plugin.GameController.LeftPanel.StartDrawPoint.Translate(
            settings.DrawXOffset.Value - plugin.GameController.IngameState.IngameUi.MapSideUI.Width);
        var drawPoint = textTopRight;
        var drawnTextVector = new List<Vector2>();

        if (isLoading)
        {
            var line = graphics.DrawText("Loading...", drawPoint, Color.Orange, FontAlign.Right);
            drawnTextVector.Add(line);
            drawPoint.Y += line.Y;
        }
        else
        {
            // The socket counts of the remnants in the zone. A remnant with N sockets can craft any
            // recipe needing up to N runes, so when filtering is on we keep rewards whose recipe size
            // is <= the largest socket count available.
            var socketCounts = plugin.EncounterSocketCounts.Distinct().ToList();
            var filter = settings.FilterBySocketCount.Value && socketCounts.Count > 0;
            var maxSockets = socketCounts.Count > 0 ? socketCounts.Max() : 0;

            if (socketCounts.Count > 0)
            {
                var summary = $"Expedition: {string.Join(", ", socketCounts.OrderBy(c => c))} runes";
                var line = graphics.DrawText(summary, drawPoint, settings.EncounterHeaderColor.Value, FontAlign.Right);
                drawnTextVector.Add(line);
                drawPoint.Y += line.Y;
            }

            var showValues = settings.ShowRewardValues.Value;

            foreach (var rune in runes)
            {
                // Collapse to one line per reward (cheapest rune count), after optionally filtering to
                // recipes craftable at this socket count, then attach each reward's NinjaPricer value.
                var rewards = (filter ? rune.Rewards.Where(o => o.RuneCount <= maxSockets) : rune.Rewards)
                    .GroupBy(o => o.Reward)
                    .Select(g => new RewardOption(g.Key, g.Min(o => o.RuneCount)))
                    .Select(o => (Option: o, Value: showValues ? plugin.GetRewardValue(o.Reward) : 0))
                    .ToList();

                // Most valuable first when pricing; otherwise cheapest recipe then name.
                rewards = showValues
                    ? rewards.OrderByDescending(r => r.Value).ThenBy(r => r.Option.Reward).ToList()
                    : rewards.OrderBy(r => r.Option.RuneCount).ThenBy(r => r.Option.Reward).ToList();

                // A known rune with nothing achievable at this socket count is just noise; hide it.
                if (!rune.IsUnknown && rewards.Count == 0)
                    continue;

                var headerColor = rune.IsUnknown ? settings.UnknownRuneColor.Value
                    : rune.IsRare ? settings.RareRuneNameColor.Value
                    : settings.RuneNameColor.Value;
                var header = rune.IsUnknown
                    ? $"Unknown rune: {rune.Name}"
                    : rune.IsRare ? $"Rare {rune.Name}  ({rewards.Count})" : $"{rune.Name}  ({rewards.Count})";
                var headerLine = graphics.DrawText(header, drawPoint, headerColor, FontAlign.Right);
                drawnTextVector.Add(headerLine);
                drawPoint.Y += headerLine.Y;

                var cap = settings.MaxRecipesPerRune.Value;
                var shown = cap > 0 ? rewards.Take(cap).ToList() : rewards;

                foreach (var (option, value) in shown)
                {
                    var text = option.Reward;
                    if (showValues && value > 0)
                        text += $"  {FormatValue(value)}";
                    var line = graphics.DrawText(text, drawPoint, settings.DefaultTextColor.Value, FontAlign.Right);
                    drawnTextVector.Add(line);
                    drawPoint.Y += line.Y;
                }

                if (cap > 0 && rewards.Count > cap)
                {
                    var more = graphics.DrawText($"… +{rewards.Count - cap} more", drawPoint, settings.DefaultTextColor.Value, FontAlign.Right);
                    drawnTextVector.Add(more);
                    drawPoint.Y += more.Y;
                }
            }
        }

        if (drawnTextVector.Count > 0)
        {
            var maxWidth = drawnTextVector.Max(v => v.X);
            var totalHeight = drawnTextVector.Sum(v => v.Y);
            float padding = settings.BackgroundPadding.Value;
            var bounds = new RectangleF(
                textTopRight.X - maxWidth - padding, textTopRight.Y - padding,
                maxWidth + padding * 2, totalHeight + padding * 2);
            graphics.DrawBox(bounds, settings.BackgroundColor, settings.BackgroundRounding.Value);
        }

        plugin.GameController.LeftPanel.StartDrawPoint = drawPoint;
    }
}
