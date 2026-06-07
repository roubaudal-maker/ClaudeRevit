using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateSelectionFilter : IRevitTool
{
    public string Name => "create_selection_filter";

    public string Description =>
        "Creates a SelectionFilterElement — a named saved selection set you can later use with " +
        "apply_filter_to_view or quickly reapply via Revit's UI. Pass a name and a list of element ids.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Filter name (unique)." }),
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Elements to include.",
                items = new { type = "integer" }
            })
        },
        Required = ["name", "element_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var name = input["name"].GetString()!;
        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var filter = SelectionFilterElement.Create(doc, name);
        filter.SetElementIds(ids);

        return JsonSerializer.Serialize(new
        {
            id = filter.Id.Value,
            name = filter.Name,
            type = "SelectionFilterElement",
            element_count = ids.Count
        });
    }
}
