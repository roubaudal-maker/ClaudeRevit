using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateRoom : IRevitTool
{
    public string Name => "create_room";

    public string Description =>
        "Places a room at a plan-coordinate point (in feet) on a named level. The point must lie inside " +
        "a closed area bounded by walls (or other room-bounding elements) for the room to compute its area. " +
        "Optional name and number can be set.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Plan X (east) in feet." }),
            ["y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Plan Y (north) in feet." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Level name." }),
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional room name." }),
            ["number"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional room number." })
        },
        Required = ["x", "y", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var x = input["x"].GetDouble();
        var y = input["y"].GetDouble();
        var levelName = input["level_name"].GetString()!;

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        var room = doc.Create.NewRoom(level, new UV(x, y))
            ?? throw new InvalidOperationException(
                "Room creation returned null. Make sure the point lies inside a closed bounded region " +
                "and that the level has 'Computation Height' set appropriately.");

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
            room.Name = n.GetString();
        if (input.TryGetValue("number", out var num) && num.ValueKind == JsonValueKind.String)
            room.Number = num.GetString();

        return JsonSerializer.Serialize(new
        {
            id = room.Id.Value,
            type = "Room",
            name = room.Name,
            number = room.Number,
            level = level.Name,
            area_sqft = room.Area
        });
    }
}
