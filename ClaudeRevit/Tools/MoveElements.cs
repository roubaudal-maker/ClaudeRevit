using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class MoveElements : IRevitTool
{
    public string Name => "move_elements";

    public string Description =>
        "Translates one or more elements by a vector (in feet). All values in feet. " +
        "Use 0 for axes that shouldn't change.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 1,
                description = "Element ids to move.",
                items = new { type = "integer" }
            }),
            ["dx_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Translation along X (east) in feet." }),
            ["dy_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Translation along Y (north) in feet." }),
            ["dz_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Translation along Z (up) in feet." })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64()))
            .ToList();

        var dx = input.TryGetValue("dx_ft", out var x) ? x.GetDouble() : 0.0;
        var dy = input.TryGetValue("dy_ft", out var y) ? y.GetDouble() : 0.0;
        var dz = input.TryGetValue("dz_ft", out var z) ? z.GetDouble() : 0.0;

        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9 && Math.Abs(dz) < 1e-9)
            throw new InvalidOperationException("Translation vector is zero — nothing to move.");

        ElementTransformUtils.MoveElements(doc, ids, new XYZ(dx, dy, dz));

        return JsonSerializer.Serialize(new
        {
            moved_count = ids.Count,
            translation_ft = new { x = dx, y = dy, z = dz }
        });
    }
}
