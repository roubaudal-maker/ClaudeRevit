using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ApplyFilterToView : IRevitTool
{
    public string Name => "apply_filter_to_view";

    public string Description =>
        "Applies a filter to a view, optionally setting visibility and graphic overrides. " +
        "If r/g/b are supplied, elements matching the filter get that color override. " +
        "If 'visible' is false, matching elements are hidden in the view.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "View id." }),
            ["filter_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Filter id." }),
            ["visible"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "Visibility of matching elements (default true)." }),
            ["r"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Red (0-255) — optional color override." }),
            ["g"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Green (0-255)." }),
            ["b"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Blue (0-255)." })
        },
        Required = ["view_id", "filter_id"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var viewId = new ElementId(input["view_id"].GetInt64());
        var filterId = new ElementId(input["filter_id"].GetInt64());

        var view = doc.GetElement(viewId) as View
            ?? throw new InvalidOperationException("view_id is not a view.");

        if (!view.IsFilterApplied(filterId))
            view.AddFilter(filterId);

        var visible = !input.TryGetValue("visible", out var v) || v.ValueKind != JsonValueKind.False;
        view.SetFilterVisibility(filterId, visible);

        var hasR = input.TryGetValue("r", out var rEl);
        var hasG = input.TryGetValue("g", out var gEl);
        var hasB = input.TryGetValue("b", out var bEl);
        var hasColor = hasR && hasG && hasB;

        if (hasColor)
        {
            var color = new Color((byte)rEl.GetInt32(), (byte)gEl.GetInt32(), (byte)bEl.GetInt32());
            var overrides = new OverrideGraphicSettings();
            overrides.SetProjectionLineColor(color);
            overrides.SetCutLineColor(color);
            overrides.SetSurfaceForegroundPatternColor(color);
            overrides.SetCutForegroundPatternColor(color);

            var solidPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);
            if (solidPattern != null)
            {
                overrides.SetSurfaceForegroundPatternId(solidPattern.Id);
                overrides.SetCutForegroundPatternId(solidPattern.Id);
            }
            view.SetFilterOverrides(filterId, overrides);
        }

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            filter_id = filterId.Value,
            visible,
            color_applied = hasColor
        });
    }
}
