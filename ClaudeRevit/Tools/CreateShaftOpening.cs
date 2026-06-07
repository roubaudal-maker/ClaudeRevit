using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateShaftOpening : IRevitTool
{
    public string Name => "create_shaft_opening";

    public string Description =>
        "Creates a vertical shaft opening from a bottom level to a top level, cutting through any floors/roofs " +
        "between them. Boundary defined by a closed loop of plan-coordinate points (in feet). Minimum 3 points.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["bottom_level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Bottom level name." }),
            ["top_level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Top level name." }),
            ["points"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 3,
                description = "Boundary points {x, y} in feet, in order. Closes automatically.",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" }
                    },
                    required = new[] { "x", "y" }
                }
            })
        },
        Required = ["bottom_level_name", "top_level_name", "points"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var bottomName = input["bottom_level_name"].GetString()!;
        var topName = input["top_level_name"].GetString()!;

        var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
        var bottom = levels.FirstOrDefault(l => l.Name == bottomName)
            ?? throw new InvalidOperationException($"Bottom level '{bottomName}' not found.");
        var top = levels.FirstOrDefault(l => l.Name == topName)
            ?? throw new InvalidOperationException($"Top level '{topName}' not found.");

        if (top.Elevation <= bottom.Elevation)
            throw new InvalidOperationException(
                $"Top level '{topName}' ({top.Elevation}) must be above bottom level '{bottomName}' ({bottom.Elevation}).");

        var pts = input["points"].EnumerateArray()
            .Select(p => new XYZ(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble(), 0))
            .ToList();
        if (pts.Count < 3)
            throw new InvalidOperationException($"Shaft needs at least 3 boundary points (got {pts.Count}).");

        var curveArray = new CurveArray();
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            if (a.IsAlmostEqualTo(b))
                throw new InvalidOperationException($"Boundary points {i} and {(i + 1) % pts.Count} are identical.");
            curveArray.Append(Line.CreateBound(a, b));
        }

        var shaft = doc.Create.NewOpening(bottom, top, curveArray);

        return JsonSerializer.Serialize(new
        {
            id = shaft.Id.Value,
            type = "ShaftOpening",
            bottom_level = bottom.Name,
            top_level = top.Name,
            boundary_points = pts.Count
        });
    }
}
