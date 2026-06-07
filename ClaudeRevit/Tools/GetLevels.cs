using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetLevels : IRevitTool
{
    public string Name => "get_levels";

    public string Description =>
        "Returns all levels in the active document with their name, id, and elevation in feet. " +
        "Call this before creating walls, floors, or other level-hosted elements when the user " +
        "hasn't specified a level by name.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>(),
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new System.InvalidOperationException("No document is open.");

        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .Select(l => new
            {
                id = l.Id.Value,
                name = l.Name,
                elevation_ft = l.Elevation
            })
            .ToList();

        return JsonSerializer.Serialize(new { count = levels.Count, levels });
    }
}
