using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetCropBox : IRevitTool
{
    public string Name => "set_crop_box";

    public string Description =>
        "Controls the crop box on a view: activate/deactivate cropping, show/hide the crop region outline, " +
        "and optionally set the crop region extents (in feet, plan coordinates). " +
        "Pass only the fields you want to change.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id (default active view)." }),
            ["active"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "Turn cropping on/off." }),
            ["visible"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "Show/hide the crop region outline." }),
            ["min_x_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Crop min X (feet)." }),
            ["min_y_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Crop min Y (feet)." }),
            ["max_x_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Crop max X (feet)." }),
            ["max_y_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Crop max Y (feet)." })
        },
        Required = []
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        View view;
        if (input.TryGetValue("view_id", out var vid) && vid.ValueKind == JsonValueKind.Number)
            view = doc.GetElement(new ElementId(vid.GetInt64())) as View
                ?? throw new InvalidOperationException("view_id is not a view.");
        else
            view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var changes = new List<string>();

        if (input.TryGetValue("active", out var a) && (a.ValueKind == JsonValueKind.True || a.ValueKind == JsonValueKind.False))
        {
            view.CropBoxActive = a.ValueKind == JsonValueKind.True;
            changes.Add("active");
        }
        if (input.TryGetValue("visible", out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
        {
            view.CropBoxVisible = v.ValueKind == JsonValueKind.True;
            changes.Add("visible");
        }

        var hasMnx = input.TryGetValue("min_x_ft", out var mnx);
        var hasMny = input.TryGetValue("min_y_ft", out var mny);
        var hasMxx = input.TryGetValue("max_x_ft", out var mxx);
        var hasMxy = input.TryGetValue("max_y_ft", out var mxy);
        if (hasMnx && hasMny && hasMxx && hasMxy)
        {
            var bbox = view.CropBox;
            bbox.Min = new XYZ(mnx.GetDouble(), mny.GetDouble(), bbox.Min.Z);
            bbox.Max = new XYZ(mxx.GetDouble(), mxy.GetDouble(), bbox.Max.Z);
            view.CropBox = bbox;
            changes.Add("extents");
        }

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            changed = changes,
            crop_active = view.CropBoxActive,
            crop_visible = view.CropBoxVisible
        });
    }
}
