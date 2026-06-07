using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetColorOverride : IRevitTool
{
    public string Name => "set_color_override";

    public string Description =>
        "Overrides the graphics of one or more elements in the active view with a single solid color (RGB 0-255). " +
        "Useful for highlighting elements visually — e.g. 'show me which walls are over 10 m long' → query then " +
        "highlight in red. Pass null/omit r,g,b to clear overrides on the listed elements.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 1,
                description = "Elements to override.",
                items = new { type = "integer" }
            }),
            ["r"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, maximum = 255, description = "Red (0-255). Omit to clear override." }),
            ["g"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, maximum = 255, description = "Green (0-255)." }),
            ["b"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, maximum = 255, description = "Blue (0-255)." })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");
        var view = doc.ActiveView
            ?? throw new InvalidOperationException("No active view.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64()))
            .ToList();

        var hasR = input.TryGetValue("r", out var rEl);
        var hasG = input.TryGetValue("g", out var gEl);
        var hasB = input.TryGetValue("b", out var bEl);
        var hasColor = hasR && hasG && hasB;

        var settings = new OverrideGraphicSettings();
        if (hasColor)
        {
            var color = new Color((byte)rEl.GetInt32(), (byte)gEl.GetInt32(), (byte)bEl.GetInt32());
            settings.SetProjectionLineColor(color);
            settings.SetCutLineColor(color);
            settings.SetSurfaceForegroundPatternColor(color);
            settings.SetCutForegroundPatternColor(color);

            var solidPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);
            if (solidPattern != null)
            {
                settings.SetSurfaceForegroundPatternId(solidPattern.Id);
                settings.SetCutForegroundPatternId(solidPattern.Id);
            }
        }

        foreach (var id in ids) view.SetElementOverrides(id, settings);

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            count = ids.Count,
            cleared = !hasColor,
            color = hasColor ? new { r = rEl.GetInt32(), g = gEl.GetInt32(), b = bEl.GetInt32() } : null
        });
    }
}
