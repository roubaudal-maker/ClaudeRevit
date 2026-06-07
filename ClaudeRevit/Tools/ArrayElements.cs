using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ArrayElements : IRevitTool
{
    public string Name => "array_elements";

    public string Description =>
        "Creates a linear array of elements. Provide the source element ids, total count, and the " +
        "displacement vector between adjacent items (in feet). Returns ids of the new copies. " +
        "The originals are retained — count includes the original.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Source elements to array.",
                items = new { type = "integer" }
            }),
            ["count"] = JsonSerializer.SerializeToElement(new
            {
                type = "integer", minimum = 2,
                description = "Total number of items, including the original."
            }),
            ["dx_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Displacement X between items (feet)." }),
            ["dy_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Displacement Y between items (feet)." }),
            ["dz_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Displacement Z between items (feet)." })
        },
        Required = ["element_ids", "count"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var count = input["count"].GetInt32();
        if (count < 2) throw new InvalidOperationException("count must be at least 2.");

        var dx = input.TryGetValue("dx_ft", out var x) ? x.GetDouble() : 0.0;
        var dy = input.TryGetValue("dy_ft", out var y) ? y.GetDouble() : 0.0;
        var dz = input.TryGetValue("dz_ft", out var z) ? z.GetDouble() : 0.0;
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9 && Math.Abs(dz) < 1e-9)
            throw new InvalidOperationException("Displacement vector is zero.");

        var displacement = new XYZ(dx, dy, dz);
        var allNewIds = new List<long>();

        // Use ElementTransformUtils.CopyElements iteratively (ungrouped — simpler than LinearArray which creates a Group)
        for (int i = 1; i < count; i++)
        {
            var step = displacement * i;
            var newIds = ElementTransformUtils.CopyElements(doc, ids, step);
            allNewIds.AddRange(newIds.Select(n => n.Value));
        }

        return JsonSerializer.Serialize(new
        {
            source_count = ids.Count,
            total_items = count,
            new_count = allNewIds.Count,
            new_ids = allNewIds.Take(50).ToList(),
            new_ids_truncated = allNewIds.Count > 50,
            displacement_ft = new { x = dx, y = dy, z = dz }
        });
    }
}
