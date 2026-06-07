using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateFilledRegion : IRevitTool
{
    public string Name => "create_filled_region";

    public string Description =>
        "Creates a filled region in the active view from a closed boundary of plan-coordinate points (in feet). " +
        "Min 3 points; closes automatically. Uses the first available filled-region type unless 'region_type_name' " +
        "is specified.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["points"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 3,
                description = "Boundary points {x, y} in feet, in order.",
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
            ["region_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional FilledRegionType name." })
        },
        Required = ["points"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");
        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var pts = input["points"].EnumerateArray()
            .Select(p => new XYZ(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble(), 0))
            .ToList();
        if (pts.Count < 3)
            throw new InvalidOperationException($"Filled region needs at least 3 points (got {pts.Count}).");

        FilledRegionType type;
        if (input.TryGetValue("region_type_name", out var rtn) && rtn.ValueKind == JsonValueKind.String)
        {
            var name = rtn.GetString();
            type = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>()
                .FirstOrDefault(t => t.Name == name)
                ?? throw new InvalidOperationException($"FilledRegionType '{name}' not found.");
        }
        else
        {
            type = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No FilledRegionType available in document.");
        }

        var loop = new CurveLoop();
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            if (a.IsAlmostEqualTo(b))
                throw new InvalidOperationException($"Boundary points {i} and {(i + 1) % pts.Count} are identical.");
            loop.Append(Line.CreateBound(a, b));
        }

        var region = FilledRegion.Create(doc, type.Id, view.Id, new List<CurveLoop> { loop });

        return JsonSerializer.Serialize(new
        {
            id = region.Id.Value,
            type = "FilledRegion",
            view = view.Name,
            region_type = type.Name,
            boundary_points = pts.Count
        });
    }
}
