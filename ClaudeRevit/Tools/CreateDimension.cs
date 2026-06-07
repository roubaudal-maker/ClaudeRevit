using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateDimension : IRevitTool
{
    public string Name => "create_dimension";

    public string Description =>
        "Creates a linear dimension in the active view between 2+ elements. Currently supports walls " +
        "(centerlines) and grids. Specify the dimension's location with a line (start and end points in feet) " +
        "— make this line roughly parallel to the dimension you want, offset away from the elements so the " +
        "text doesn't overlap. The elements being dimensioned must be visible in the active view.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 2,
                description = "Walls or grids to dimension. At least 2 required.",
                items = new { type = "integer" }
            }),
            ["line_start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Dim line start X (feet)." }),
            ["line_start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Dim line start Y (feet)." }),
            ["line_end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Dim line end X (feet)." }),
            ["line_end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Dim line end Y (feet)." })
        },
        Required = ["element_ids", "line_start_x", "line_start_y", "line_end_x", "line_end_y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");
        var view = doc.ActiveView
            ?? throw new InvalidOperationException("No active view.");

        var elementIds = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64()))
            .ToList();
        if (elementIds.Count < 2)
            throw new InvalidOperationException(
                $"Dimension needs at least 2 elements (got {elementIds.Count}).");

        var sx = input["line_start_x"].GetDouble();
        var sy = input["line_start_y"].GetDouble();
        var ex = input["line_end_x"].GetDouble();
        var ey = input["line_end_y"].GetDouble();

        var start = new XYZ(sx, sy, 0);
        var end = new XYZ(ex, ey, 0);
        if (start.IsAlmostEqualTo(end))
            throw new InvalidOperationException("Dimension line has zero length.");
        var dimLine = Line.CreateBound(start, end);

        var refs = new ReferenceArray();
        var skipped = new List<object>();

        foreach (var id in elementIds)
        {
            var el = doc.GetElement(id);
            if (el == null)
            {
                skipped.Add(new { id = id.Value, reason = "not found" });
                continue;
            }

            Reference? reference = null;
            string? reason = null;

            if (el is Wall wall && wall.Location is LocationCurve wallLc)
            {
                reference = wallLc.Curve.Reference;
                if (reference == null) reason = "wall location curve has no reference";
            }
            else if (el is Grid grid)
            {
                reference = grid.Curve?.Reference;
                if (reference == null) reason = "grid has no curve reference";
            }
            else
            {
                reason = $"unsupported category: {el.Category?.Name ?? "(none)"} — only walls and grids supported";
            }

            if (reference == null)
            {
                skipped.Add(new { id = id.Value, reason = reason ?? "could not get reference" });
                continue;
            }

            refs.Append(reference);
        }

        if (refs.Size < 2)
            throw new InvalidOperationException(
                $"Got only {refs.Size} valid reference(s); need at least 2. Skipped: {JsonSerializer.Serialize(skipped)}");

        var dim = doc.Create.NewDimension(view, dimLine, refs);

        return JsonSerializer.Serialize(new
        {
            id = dim.Id.Value,
            type = "Dimension",
            view = view.Name,
            referenced_count = refs.Size,
            value_ft = dim.Value,
            skipped
        });
    }
}
