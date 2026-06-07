using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ListLoadedFamilies : IRevitTool
{
    public string Name => "list_loaded_families";

    public string Description =>
        "Lists all loadable families currently in the document, with id, name, and category. " +
        "Optionally filter by category. Useful before placing instances or duplicating types.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["category"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional category filter, e.g. 'Doors', 'Furniture'." }),
            ["limit"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Max results (default 200, max 1000).", minimum = 1, maximum = 1000 })
        },
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var limit = input.TryGetValue("limit", out var l) ? l.GetInt32() : 200;
        if (limit < 1 || limit > 1000) limit = 200;

        BuiltInCategory? filter = null;
        if (input.TryGetValue("category", out var c) && c.ValueKind == JsonValueKind.String)
        {
            if (Enum.TryParse<BuiltInCategory>($"OST_{c.GetString()}", true, out var bic))
                filter = bic;
        }

        var allFamilies = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>();

        var filtered = (filter.HasValue
            ? allFamilies.Where(f => f.FamilyCategory != null &&
                                     f.FamilyCategory.Id.Value == (long)filter.Value)
            : allFamilies)
            .Take(limit + 1)
            .ToList();

        var truncated = filtered.Count > limit;

        return JsonSerializer.Serialize(new
        {
            count = Math.Min(filtered.Count, limit),
            truncated,
            families = filtered.Take(limit).Select(f => new
            {
                id = f.Id.Value,
                name = f.Name,
                category = f.FamilyCategory?.Name,
                type_count = f.GetFamilySymbolIds().Count
            })
        });
    }
}
