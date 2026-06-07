using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class MirrorElements : IRevitTool
{
    public string Name => "mirror_elements";

    public string Description =>
        "Mirrors elements across a vertical mirror plane defined by two plan-coordinate points (in feet). " +
        "If 'copy' is true, mirrored copies are created (originals retained); if false, the elements are mirrored " +
        "in place. Use for symmetric layouts.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Elements to mirror.",
                items = new { type = "integer" }
            }),
            ["line_start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Mirror line start X (feet)." }),
            ["line_start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Mirror line start Y (feet)." }),
            ["line_end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Mirror line end X (feet)." }),
            ["line_end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Mirror line end Y (feet)." }),
            ["copy"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "If true, keep originals and add mirrored copies (default true)." })
        },
        Required = ["element_ids", "line_start_x", "line_start_y", "line_end_x", "line_end_y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var sx = input["line_start_x"].GetDouble();
        var sy = input["line_start_y"].GetDouble();
        var ex = input["line_end_x"].GetDouble();
        var ey = input["line_end_y"].GetDouble();
        var copy = !input.TryGetValue("copy", out var c) || c.ValueKind != JsonValueKind.False;

        var lineDir = new XYZ(ex - sx, ey - sy, 0);
        if (lineDir.IsZeroLength())
            throw new InvalidOperationException("Mirror line has zero length.");
        // Plane normal is perpendicular to line direction in plan, horizontal
        var normal = new XYZ(-lineDir.Y, lineDir.X, 0).Normalize();
        var origin = new XYZ(sx, sy, 0);
        var plane = Plane.CreateByNormalAndOrigin(normal, origin);

        if (copy)
            ElementTransformUtils.MirrorElements(doc, ids, plane, true);
        else
            ElementTransformUtils.MirrorElements(doc, ids, plane, false);

        return JsonSerializer.Serialize(new
        {
            mirrored_count = ids.Count,
            kept_originals = copy,
            mirror_line = new { from = new { x = sx, y = sy }, to = new { x = ex, y = ey } }
        });
    }
}
