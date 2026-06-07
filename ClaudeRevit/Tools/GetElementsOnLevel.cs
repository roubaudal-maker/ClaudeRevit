using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetElementsOnLevel : IRevitTool
{
    public string Name => "get_elements_on_level";

    public string Description =>
        "Returns all elements associated with a given level (LevelId == level). Optionally filter by category.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["level_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Level id." }),
            ["category"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional category to filter (e.g. 'Walls')." }),
            ["limit"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Max results (default 200, max 1000).", minimum = 1, maximum = 1000 })
        },
        Required = ["level_id"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var levelId = new ElementId(input["level_id"].GetInt64());
        var level = doc.GetElement(levelId) as Level
            ?? throw new InvalidOperationException($"Element {levelId.Value} is not a Level.");

        var limit = input.TryGetValue("limit", out var l) ? l.GetInt32() : 200;
        if (limit < 1 || limit > 1000) limit = 200;

        FilteredElementCollector collector;
        if (input.TryGetValue("category", out var c) && c.ValueKind == JsonValueKind.String)
        {
            if (!Enum.TryParse<BuiltInCategory>($"OST_{c.GetString()}", true, out var bic))
                throw new InvalidOperationException($"Unknown category '{c.GetString()}'.");
            collector = new FilteredElementCollector(doc).OfCategory(bic);
        }
        else
        {
            collector = new FilteredElementCollector(doc);
        }

        var raw = collector
            .WhereElementIsNotElementType()
            .Where(e => e.LevelId == levelId)
            .Take(limit + 1)
            .ToList();
        var truncated = raw.Count > limit;

        return JsonSerializer.Serialize(new
        {
            level = level.Name,
            count = Math.Min(raw.Count, limit),
            truncated,
            elements = raw.Take(limit).Select(e => new
            {
                id = e.Id.Value,
                category = e.Category?.Name,
                name = e.Name
            })
        });
    }
}
