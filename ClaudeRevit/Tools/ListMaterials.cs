using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ListMaterials : IRevitTool
{
    public string Name => "list_materials";

    public string Description =>
        "Lists all materials in the document with id, name, and material category.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["limit"] = JsonSerializer.SerializeToElement(new
            {
                type = "integer",
                description = "Max results (default 200, max 1000).",
                minimum = 1, maximum = 1000
            })
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

        var raw = new FilteredElementCollector(doc)
            .OfClass(typeof(Material))
            .Cast<Material>()
            .Take(limit + 1)
            .ToList();

        var truncated = raw.Count > limit;

        return JsonSerializer.Serialize(new
        {
            count = Math.Min(raw.Count, limit),
            truncated,
            materials = raw.Take(limit).Select(m => new
            {
                id = m.Id.Value,
                name = m.Name,
                category = m.MaterialCategory,
                class_name = m.MaterialClass
            })
        });
    }
}
