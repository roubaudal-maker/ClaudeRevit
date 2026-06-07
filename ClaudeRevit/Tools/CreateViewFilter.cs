using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateViewFilter : IRevitTool
{
    public string Name => "create_view_filter";

    public string Description =>
        "Creates a ParameterFilterElement that matches all elements in the given categories (no rules). " +
        "Apply it to a view with apply_filter_to_view to control visibility or graphics. " +
        "For rule-based filters, that comes later — for now this is a category-only filter.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Filter name (unique)." }),
            ["categories"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Categories to include, e.g. ['Walls', 'Doors'].",
                items = new { type = "string" }
            })
        },
        Required = ["name", "categories"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var name = input["name"].GetString()!;
        var categoryNames = input["categories"].EnumerateArray()
            .Select(e => e.GetString()!).ToList();

        var catIds = new List<ElementId>();
        var unknown = new List<string>();
        foreach (var c in categoryNames)
        {
            if (Enum.TryParse<BuiltInCategory>($"OST_{c}", true, out var bic))
                catIds.Add(new ElementId(bic));
            else
                unknown.Add(c);
        }

        if (catIds.Count == 0)
            throw new InvalidOperationException(
                $"No valid categories. Unknown: {string.Join(", ", unknown)}");

        ParameterFilterElement filter;
        try
        {
            filter = ParameterFilterElement.Create(doc, name, catIds);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not create filter — one or more categories may not be filterable, " +
                $"or the name '{name}' may already exist. Revit said: {ex.Message}");
        }

        return JsonSerializer.Serialize(new
        {
            id = filter.Id.Value,
            name = filter.Name,
            type = "ParameterFilterElement",
            category_count = catIds.Count,
            unknown_categories = unknown
        });
    }
}
