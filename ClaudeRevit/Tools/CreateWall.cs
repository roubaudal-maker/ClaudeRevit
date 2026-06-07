using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateWall : IRevitTool
{
    public string Name => "create_wall";

    public string Description =>
        "Creates a single straight wall between two plan-coordinate points (in feet) on a named level. " +
        "All spatial values are in feet — convert from meters/mm before calling. " +
        "Use get_levels first if the user did not specify a level.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start point X (east) in feet." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start point Y (north) in feet." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End point X in feet." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End point Y in feet." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Name of the base level (must match exactly)." }),
            ["height_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Wall height in feet (default 10).", minimum = 0.1 }),
            ["wall_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional wall type name. Defaults to first available wall type." })
        },
        Required = ["start_x", "start_y", "end_x", "end_y", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var startX = input["start_x"].GetDouble();
        var startY = input["start_y"].GetDouble();
        var endX = input["end_x"].GetDouble();
        var endY = input["end_y"].GetDouble();
        var levelName = input["level_name"].GetString()!;
        var height = input.TryGetValue("height_ft", out var h) ? h.GetDouble() : 10.0;

        var level = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException(
                $"Level '{levelName}' not found. Call get_levels to see available levels.");

        WallType wallType;
        if (input.TryGetValue("wall_type_name", out var wt) && wt.ValueKind == JsonValueKind.String)
        {
            var wtName = wt.GetString();
            wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(t => t.Name == wtName)
                ?? throw new InvalidOperationException($"Wall type '{wtName}' not found.");
        }
        else
        {
            wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First();
        }

        var start = new XYZ(startX, startY, 0);
        var end = new XYZ(endX, endY, 0);
        if (start.IsAlmostEqualTo(end))
            throw new InvalidOperationException("Start and end points are identical — wall has zero length.");

        var curve = Line.CreateBound(start, end);
        var wall = Wall.Create(doc, curve, wallType.Id, level.Id, height, 0, false, false);

        return JsonSerializer.Serialize(new
        {
            id = wall.Id.Value,
            type = "Wall",
            type_name = wallType.Name,
            level = level.Name,
            length_ft = curve.Length,
            height_ft = height
        });
    }
}
