using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateLevel : IRevitTool
{
    public string Name => "create_level";

    public string Description =>
        "Creates a new level at a given elevation in feet. Use this when the user asks for a new floor, " +
        "story, or level. Elevation values are in feet (Revit's internal unit) — convert from meters before calling.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Level name (must be unique)." }),
            ["elevation_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Elevation in feet from Project Base Point." })
        },
        Required = ["name", "elevation_ft"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var name = input["name"].GetString()!;
        var elevation = input["elevation_ft"].GetDouble();

        if (new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .Any(l => l.Name == name))
            throw new InvalidOperationException($"A level named '{name}' already exists.");

        var level = Level.Create(doc, elevation);
        level.Name = name;

        return JsonSerializer.Serialize(new
        {
            id = level.Id.Value,
            type = "Level",
            name = level.Name,
            elevation_ft = level.Elevation
        });
    }
}
