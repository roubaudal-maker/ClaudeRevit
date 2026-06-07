using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ListFamilyTypes : IRevitTool
{
    public string Name => "list_family_types";

    public string Description =>
        "Lists family symbols (types) loaded in the document for a given category. Common categories: " +
        "'Doors', 'Windows', 'Furniture', 'Walls', 'Floors', 'Roofs', 'Stairs', 'Columns', 'GenericModel'. " +
        "Returns each type's family name + type name + id, useful before placing instances.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["category"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Category, e.g. 'Doors', 'Windows', 'Walls'."
            }),
            ["limit"] = JsonSerializer.SerializeToElement(new
            {
                type = "integer",
                description = "Max results (default 100, max 500).",
                minimum = 1, maximum = 500
            })
        },
        Required = ["category"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var category = input["category"].GetString()!;
        var limit = input.TryGetValue("limit", out var l) ? l.GetInt32() : 100;
        if (limit < 1 || limit > 500) limit = 100;

        if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", ignoreCase: true, out var bic))
            throw new InvalidOperationException($"Unknown category '{category}'.");

        IEnumerable<ElementType> types;

        // Most family-instance categories use FamilySymbol; system family categories use specific types
        var familySymbols = new FilteredElementCollector(doc)
            .OfCategory(bic)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .ToList();

        if (familySymbols.Count > 0)
        {
            types = familySymbols.Cast<ElementType>();
        }
        else
        {
            types = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsElementType()
                .Cast<ElementType>();
        }

        var rows = types.Take(limit + 1).ToList();
        var truncated = rows.Count > limit;

        return JsonSerializer.Serialize(new
        {
            category,
            count = Math.Min(rows.Count, limit),
            truncated,
            types = rows.Take(limit).Select(t => new
            {
                id = t.Id.Value,
                family = t.FamilyName,
                name = t.Name
            })
        });
    }
}
