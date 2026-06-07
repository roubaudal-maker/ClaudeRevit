using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetElementBoundingBox : IRevitTool
{
    public string Name => "get_element_bounding_box";

    public string Description =>
        "Returns the model-coordinate bounding box (min/max in feet) of one or more elements. " +
        "Useful for measuring extents, finding centers, or sizing things relative to existing geometry.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 1,
                description = "Elements to query.",
                items = new { type = "integer" }
            })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var results = ids.Select(id =>
        {
            var el = doc.GetElement(id);
            if (el == null) return new { id = id.Value, error = "not found", min_ft = (object?)null, max_ft = (object?)null, center_ft = (object?)null, size_ft = (object?)null };
            var bbox = el.get_BoundingBox(null);
            if (bbox == null) return new { id = id.Value, error = "no bounding box", min_ft = (object?)null, max_ft = (object?)null, center_ft = (object?)null, size_ft = (object?)null };
            var min = bbox.Min;
            var max = bbox.Max;
            var center = (min + max) * 0.5;
            return new
            {
                id = id.Value,
                error = (string?)null,
                min_ft = (object?)new { x = min.X, y = min.Y, z = min.Z },
                max_ft = (object?)new { x = max.X, y = max.Y, z = max.Z },
                center_ft = (object?)new { x = center.X, y = center.Y, z = center.Z },
                size_ft = (object?)new { x = max.X - min.X, y = max.Y - min.Y, z = max.Z - min.Z }
            };
        }).ToList();

        return JsonSerializer.Serialize(results);
    }
}
