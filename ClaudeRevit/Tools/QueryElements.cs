using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class QueryElements : IRevitTool
{
    public string Name => "query_elements";

    public string Description =>
        "Lists elements in the active document filtered by category. Returns id, name, type, " +
        "and host level (when applicable) for each element. Common categories: 'Walls', 'Floors', " +
        "'Doors', 'Windows', 'Roofs', 'Stairs', 'Columns', 'Furniture', 'Levels', 'Grids', 'Rooms'.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["category"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Revit category, e.g. 'Walls', 'Floors', 'Doors'."
            }),
            ["limit"] = JsonSerializer.SerializeToElement(new
            {
                type = "integer",
                description = "Maximum number of elements to return (default 50, max 500).",
                minimum = 1,
                maximum = 500
            })
        },
        Required = ["category"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var category = input["category"].GetString()
            ?? throw new InvalidOperationException("category is required.");

        var limit = input.TryGetValue("limit", out var l) ? l.GetInt32() : 50;
        if (limit < 1 || limit > 500) limit = 50;

        if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", ignoreCase: true, out var bic))
            throw new InvalidOperationException(
                $"Unknown category '{category}'. Try 'Walls', 'Floors', 'Doors', 'Windows', etc.");

        var raw = new FilteredElementCollector(doc)
            .OfCategory(bic)
            .WhereElementIsNotElementType()
            .Take(limit + 1)
            .ToList();

        var truncated = raw.Count > limit;
        var elements = raw.Take(limit).Select(e => new
        {
            id = e.Id.Value,
            name = e.Name,
            type_name = doc.GetElement(e.GetTypeId())?.Name,
            level = e.LevelId != ElementId.InvalidElementId
                ? doc.GetElement(e.LevelId)?.Name
                : null
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            category,
            count = elements.Count,
            truncated,
            elements
        });
    }
}
