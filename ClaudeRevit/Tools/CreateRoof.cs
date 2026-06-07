using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateRoof : IRevitTool
{
    public string Name => "create_roof";

    public string Description =>
        "Creates a flat footprint roof from a closed boundary of plan-coordinate points (in feet) on a named level. " +
        "Provide points in order; the boundary closes automatically. Minimum 3 points. " +
        "All spatial values are in feet.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["points"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 3,
                description = "Boundary points. Each is { \"x\": <feet>, \"y\": <feet> }.",
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
            }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Host level name." }),
            ["roof_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional roof type name. Defaults to first available." })
        },
        Required = ["points", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var levelName = input["level_name"].GetString()!;

        var pts = input["points"].EnumerateArray()
            .Select(p => new XYZ(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble(), 0))
            .ToList();
        if (pts.Count < 3)
            throw new InvalidOperationException($"Roof requires at least 3 points (got {pts.Count}).");

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        RoofType roofType;
        if (input.TryGetValue("roof_type_name", out var rt) && rt.ValueKind == JsonValueKind.String)
        {
            var name = rt.GetString();
            roofType = new FilteredElementCollector(doc).OfClass(typeof(RoofType)).Cast<RoofType>()
                .FirstOrDefault(t => t.Name == name)
                ?? throw new InvalidOperationException($"Roof type '{name}' not found.");
        }
        else
        {
            roofType = new FilteredElementCollector(doc).OfClass(typeof(RoofType)).Cast<RoofType>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No roof types are loaded in this document.");
        }

        var curveArray = new CurveArray();
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            if (a.IsAlmostEqualTo(b))
                throw new InvalidOperationException($"Boundary points {i} and {(i + 1) % pts.Count} are identical.");
            curveArray.Append(Line.CreateBound(a, b));
        }

        var roof = doc.Create.NewFootPrintRoof(curveArray, level, roofType, out _);

        return JsonSerializer.Serialize(new
        {
            id = roof.Id.Value,
            type = "Roof",
            type_name = roofType.Name,
            level = level.Name,
            boundary_points = pts.Count
        });
    }
}
