using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class AddCurtainGrid : IRevitTool
{
    public string Name => "add_curtain_grid";

    public string Description =>
        "Adds a grid line to an existing curtain wall. 'orientation' is 'vertical' (a U-grid line) or " +
        "'horizontal' (a V-grid line). The grid line passes through the given world-coordinate point.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["curtain_wall_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Curtain wall id." }),
            ["orientation"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "'vertical' (mullion-like) or 'horizontal' (transom-like).",
                @enum = new[] { "vertical", "horizontal" }
            }),
            ["x_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Point X (feet) through which the grid passes." }),
            ["y_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Point Y (feet)." }),
            ["z_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Point Z (feet)." })
        },
        Required = ["curtain_wall_id", "orientation", "x_ft", "y_ft", "z_ft"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var wallId = new ElementId(input["curtain_wall_id"].GetInt64());
        var wall = doc.GetElement(wallId) as Wall
            ?? throw new InvalidOperationException($"Element {wallId.Value} is not a Wall.");

        var grid = wall.CurtainGrid
            ?? throw new InvalidOperationException($"Wall '{wall.Name}' has no curtain grid (not a curtain wall).");

        var isUGrid = input["orientation"].GetString()!.Equals("vertical", StringComparison.OrdinalIgnoreCase);
        var pt = new XYZ(input["x_ft"].GetDouble(), input["y_ft"].GetDouble(), input["z_ft"].GetDouble());

        var line = grid.AddGridLine(isUGrid, pt, false);

        return JsonSerializer.Serialize(new
        {
            id = line.Id.Value,
            type = "CurtainGridLine",
            wall = wall.Name,
            orientation = input["orientation"].GetString(),
            point_ft = new { x = pt.X, y = pt.Y, z = pt.Z }
        });
    }
}
