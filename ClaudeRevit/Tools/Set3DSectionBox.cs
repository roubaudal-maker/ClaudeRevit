using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class Set3DSectionBox : IRevitTool
{
    public string Name => "set_3d_section_box";

    public string Description =>
        "Enables and sets a section box on a 3D view, clipping it to a rectangular volume " +
        "(min/max XYZ in feet). Useful for showing a single floor or zone in a 3D view.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional 3D view id (default active view)." }),
            ["min_x_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["min_y_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["min_z_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["max_x_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["max_y_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["max_z_ft"] = JsonSerializer.SerializeToElement(new { type = "number" })
        },
        Required = ["min_x_ft", "min_y_ft", "min_z_ft", "max_x_ft", "max_y_ft", "max_z_ft"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        View3D view;
        if (input.TryGetValue("view_id", out var vid) && vid.ValueKind == JsonValueKind.Number)
            view = doc.GetElement(new ElementId(vid.GetInt64())) as View3D
                ?? throw new InvalidOperationException("view_id is not a 3D view.");
        else
            view = doc.ActiveView as View3D
                ?? throw new InvalidOperationException("Active view is not a 3D view.");

        var min = new XYZ(input["min_x_ft"].GetDouble(), input["min_y_ft"].GetDouble(), input["min_z_ft"].GetDouble());
        var max = new XYZ(input["max_x_ft"].GetDouble(), input["max_y_ft"].GetDouble(), input["max_z_ft"].GetDouble());
        if (max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z)
            throw new InvalidOperationException("Each max must be greater than its corresponding min.");

        view.SetSectionBox(new BoundingBoxXYZ { Min = min, Max = max });
        view.IsSectionBoxActive = true;

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            section_box_min = new { x = min.X, y = min.Y, z = min.Z },
            section_box_max = new { x = max.X, y = max.Y, z = max.Z }
        });
    }
}
