using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class RotateElements : IRevitTool
{
    public string Name => "rotate_elements";

    public string Description =>
        "Rotates one or more elements around a vertical (Z) axis at a given plan-coordinate pivot point, " +
        "by a given angle in degrees (counter-clockwise positive). Pivot in feet.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 1,
                description = "Element ids to rotate.",
                items = new { type = "integer" }
            }),
            ["pivot_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Pivot X in feet." }),
            ["pivot_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Pivot Y in feet." }),
            ["angle_deg"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Rotation in degrees (CCW positive)." })
        },
        Required = ["element_ids", "pivot_x", "pivot_y", "angle_deg"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64()))
            .ToList();

        var px = input["pivot_x"].GetDouble();
        var py = input["pivot_y"].GetDouble();
        var deg = input["angle_deg"].GetDouble();
        var rad = deg * Math.PI / 180.0;

        var axis = Line.CreateBound(new XYZ(px, py, 0), new XYZ(px, py, 1));
        ElementTransformUtils.RotateElements(doc, ids, axis, rad);

        return JsonSerializer.Serialize(new
        {
            rotated_count = ids.Count,
            pivot_ft = new { x = px, y = py },
            angle_deg = deg
        });
    }
}
