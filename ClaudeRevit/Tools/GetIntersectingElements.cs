using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetIntersectingElements : IRevitTool
{
    public string Name => "get_intersecting_elements";

    public string Description =>
        "Returns all elements whose bounding box intersects a given 3D region (min/max XYZ in feet). " +
        "Optionally filter by category. Useful for collision-like queries.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["min_x_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["min_y_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["min_z_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["max_x_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["max_y_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["max_z_ft"] = JsonSerializer.SerializeToElement(new { type = "number" }),
            ["category"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional category filter." }),
            ["limit"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Max results (default 200).", minimum = 1, maximum = 1000 })
        },
        Required = ["min_x_ft", "min_y_ft", "min_z_ft", "max_x_ft", "max_y_ft", "max_z_ft"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var min = new XYZ(input["min_x_ft"].GetDouble(), input["min_y_ft"].GetDouble(), input["min_z_ft"].GetDouble());
        var max = new XYZ(input["max_x_ft"].GetDouble(), input["max_y_ft"].GetDouble(), input["max_z_ft"].GetDouble());

        var outline = new Outline(min, max);
        var filter = new BoundingBoxIntersectsFilter(outline);

        var limit = input.TryGetValue("limit", out var l) ? l.GetInt32() : 200;
        if (limit < 1 || limit > 1000) limit = 200;

        FilteredElementCollector collector;
        if (input.TryGetValue("category", out var c) && c.ValueKind == JsonValueKind.String)
        {
            if (!Enum.TryParse<BuiltInCategory>($"OST_{c.GetString()}", true, out var bic))
                throw new InvalidOperationException($"Unknown category '{c.GetString()}'.");
            collector = new FilteredElementCollector(doc).OfCategory(bic);
        }
        else
        {
            collector = new FilteredElementCollector(doc);
        }

        var raw = collector
            .WhereElementIsNotElementType()
            .WherePasses(filter)
            .Take(limit + 1)
            .ToList();
        var truncated = raw.Count > limit;

        return JsonSerializer.Serialize(new
        {
            count = Math.Min(raw.Count, limit),
            truncated,
            elements = raw.Take(limit).Select(e => new
            {
                id = e.Id.Value,
                category = e.Category?.Name,
                name = e.Name
            })
        });
    }
}
