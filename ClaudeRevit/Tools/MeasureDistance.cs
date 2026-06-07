using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class MeasureDistance : IRevitTool
{
    public string Name => "measure_distance";

    public string Description =>
        "Measures the distance between two points (in feet) OR between the bounding-box centers of two elements. " +
        "Pass either { from_x, from_y, from_z, to_x, to_y, to_z } for points OR { from_id, to_id } for elements.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["from_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "From point X (feet)." }),
            ["from_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "From point Y (feet)." }),
            ["from_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "From point Z (feet, default 0)." }),
            ["to_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "To point X (feet)." }),
            ["to_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "To point Y (feet)." }),
            ["to_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "To point Z (feet, default 0)." }),
            ["from_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "From element id (uses bbox center)." }),
            ["to_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "To element id (uses bbox center)." })
        },
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        XYZ from, to;

        if (input.TryGetValue("from_id", out var fid) && input.TryGetValue("to_id", out var tid))
        {
            var fromEl = doc.GetElement(new ElementId(fid.GetInt64()))
                ?? throw new InvalidOperationException($"Element {fid.GetInt64()} not found.");
            var toEl = doc.GetElement(new ElementId(tid.GetInt64()))
                ?? throw new InvalidOperationException($"Element {tid.GetInt64()} not found.");
            var fromBbox = fromEl.get_BoundingBox(null) ?? throw new InvalidOperationException("'from' element has no bounding box.");
            var toBbox = toEl.get_BoundingBox(null) ?? throw new InvalidOperationException("'to' element has no bounding box.");
            from = (fromBbox.Min + fromBbox.Max) * 0.5;
            to = (toBbox.Min + toBbox.Max) * 0.5;
        }
        else if (input.ContainsKey("from_x") && input.ContainsKey("to_x"))
        {
            from = new XYZ(
                input["from_x"].GetDouble(),
                input["from_y"].GetDouble(),
                input.TryGetValue("from_z", out var fz) ? fz.GetDouble() : 0);
            to = new XYZ(
                input["to_x"].GetDouble(),
                input["to_y"].GetDouble(),
                input.TryGetValue("to_z", out var tz) ? tz.GetDouble() : 0);
        }
        else
        {
            throw new InvalidOperationException(
                "Provide either {from_x, from_y, to_x, to_y} or {from_id, to_id}.");
        }

        var d = (to - from);
        return JsonSerializer.Serialize(new
        {
            distance_ft = d.GetLength(),
            distance_m = d.GetLength() / 3.28084,
            dx_ft = d.X,
            dy_ft = d.Y,
            dz_ft = d.Z,
            from_ft = new { x = from.X, y = from.Y, z = from.Z },
            to_ft = new { x = to.X, y = to.Y, z = to.Z }
        });
    }
}
