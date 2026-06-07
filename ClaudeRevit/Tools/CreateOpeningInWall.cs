using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateOpeningInWall : IRevitTool
{
    public string Name => "create_opening_in_wall";

    public string Description =>
        "Creates a rectangular opening in a wall defined by two diagonal corner points (in feet, world coordinates). " +
        "The two points must lie on the wall's reference plane and define a rectangle. Use this for openings " +
        "that aren't doors or windows (e.g., custom shaft, pass-through).";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["wall_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Host wall id." }),
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "First corner X (feet)." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "First corner Y (feet)." }),
            ["start_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "First corner Z (feet)." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Opposite corner X (feet)." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Opposite corner Y (feet)." }),
            ["end_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Opposite corner Z (feet)." })
        },
        Required = ["wall_id", "start_x", "start_y", "start_z", "end_x", "end_y", "end_z"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var wallId = new ElementId(input["wall_id"].GetInt64());
        var wall = doc.GetElement(wallId) as Wall
            ?? throw new InvalidOperationException($"Element {wallId.Value} is not a wall.");

        var p1 = new XYZ(
            input["start_x"].GetDouble(),
            input["start_y"].GetDouble(),
            input["start_z"].GetDouble());
        var p2 = new XYZ(
            input["end_x"].GetDouble(),
            input["end_y"].GetDouble(),
            input["end_z"].GetDouble());

        var opening = doc.Create.NewOpening(wall, p1, p2);

        return JsonSerializer.Serialize(new
        {
            id = opening.Id.Value,
            type = "Opening",
            wall = wall.Name,
            corners_ft = new { from = new { x = p1.X, y = p1.Y, z = p1.Z }, to = new { x = p2.X, y = p2.Y, z = p2.Z } }
        });
    }
}
