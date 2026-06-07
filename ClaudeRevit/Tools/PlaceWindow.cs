using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class PlaceWindow : IRevitTool
{
    public string Name => "place_window";

    public string Description =>
        "Places a window instance hosted on a wall. Specify the host wall id, the plan-coordinate " +
        "position in feet, and the level. Optionally specify a window type by name. All spatial values are in feet.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["host_wall_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Element id of the wall to host the window." }),
            ["x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Plan X position (east) in feet." }),
            ["y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Plan Y position (north) in feet." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Level name." }),
            ["window_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional window type name. Defaults to first available." })
        },
        Required = ["host_wall_id", "x", "y", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var hostId = new ElementId(input["host_wall_id"].GetInt64());
        var host = doc.GetElement(hostId) as Wall
            ?? throw new InvalidOperationException(
                $"Element {hostId.Value} is not a wall (or doesn't exist).");

        var x = input["x"].GetDouble();
        var y = input["y"].GetDouble();
        var levelName = input["level_name"].GetString()!;

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        FamilySymbol symbol;
        if (input.TryGetValue("window_type_name", out var wt) && wt.ValueKind == JsonValueKind.String)
        {
            var name = wt.GetString();
            symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name == name)
                ?? throw new InvalidOperationException($"Window type '{name}' not found.");
        }
        else
        {
            symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .First();
        }

        if (!symbol.IsActive) symbol.Activate();

        var instance = doc.Create.NewFamilyInstance(
            new XYZ(x, y, 0), symbol, host, level, StructuralType.NonStructural);

        return JsonSerializer.Serialize(new
        {
            id = instance.Id.Value,
            type = "Window",
            type_name = symbol.Name,
            host_wall_id = host.Id.Value,
            level = level.Name
        });
    }
}
