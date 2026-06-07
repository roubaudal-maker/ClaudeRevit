using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateCurtainWall : IRevitTool
{
    public string Name => "create_curtain_wall";

    public string Description =>
        "Creates a curtain wall between two plan-coordinate points (in feet) on a named level. " +
        "Uses the first available curtain-wall type unless 'wall_type_name' is provided. " +
        "Default height: 10 ft.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start X (feet)." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start Y (feet)." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End X (feet)." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End Y (feet)." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Base level name." }),
            ["height_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Wall height in feet (default 10).", minimum = 0.1 }),
            ["wall_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional curtain-wall type name." })
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

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        WallType wallType;
        if (input.TryGetValue("wall_type_name", out var wtn) && wtn.ValueKind == JsonValueKind.String)
        {
            var name = wtn.GetString();
            wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>()
                .FirstOrDefault(t => t.Name == name && t.Kind == WallKind.Curtain)
                ?? throw new InvalidOperationException($"Curtain-wall type '{name}' not found.");
        }
        else
        {
            wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>()
                .FirstOrDefault(t => t.Kind == WallKind.Curtain)
                ?? throw new InvalidOperationException("No curtain-wall types loaded.");
        }

        var start = new XYZ(startX, startY, 0);
        var end = new XYZ(endX, endY, 0);
        if (start.IsAlmostEqualTo(end))
            throw new InvalidOperationException("Wall has zero length.");

        var curve = Line.CreateBound(start, end);
        var wall = Wall.Create(doc, curve, wallType.Id, level.Id, height, 0, false, false);

        return JsonSerializer.Serialize(new
        {
            id = wall.Id.Value,
            type = "CurtainWall",
            type_name = wallType.Name,
            level = level.Name,
            length_ft = curve.Length,
            height_ft = height
        });
    }
}
