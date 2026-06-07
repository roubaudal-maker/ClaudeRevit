using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateGrid : IRevitTool
{
    public string Name => "create_grid";

    public string Description =>
        "Creates a straight grid line between two plan-coordinate points (in feet) and optionally names it. " +
        "All spatial values are in feet. Standard grid naming: vertical grids 'A', 'B', 'C'…; horizontal grids '1', '2', '3'.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start X (east) in feet." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start Y (north) in feet." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End X in feet." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End Y in feet." }),
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional grid name (e.g. 'A', '1'). Must be unique." })
        },
        Required = ["start_x", "start_y", "end_x", "end_y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var start = new XYZ(input["start_x"].GetDouble(), input["start_y"].GetDouble(), 0);
        var end = new XYZ(input["end_x"].GetDouble(), input["end_y"].GetDouble(), 0);
        if (start.IsAlmostEqualTo(end))
            throw new InvalidOperationException("Start and end points are identical.");

        var grid = Grid.Create(doc, Line.CreateBound(start, end));

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { grid.Name = n.GetString(); }
            catch (Exception ex) { throw new InvalidOperationException($"Could not set grid name: {ex.Message}"); }
        }

        return JsonSerializer.Serialize(new
        {
            id = grid.Id.Value,
            type = "Grid",
            name = grid.Name,
            length_ft = Line.CreateBound(start, end).Length
        });
    }
}
