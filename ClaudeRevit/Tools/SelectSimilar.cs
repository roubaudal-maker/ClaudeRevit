using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SelectSimilar : IRevitTool
{
    public string Name => "select_similar";

    public string Description =>
        "Selects all elements in the document that share the given element's category and type. " +
        "Equivalent to Revit's right-click → 'Select All Instances → In Entire Project'. " +
        "Returns the resulting selection ids.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Template element — selection matches its category AND type." }),
            ["visible_only"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "Limit to elements visible in the active view (default false)." })
        },
        Required = ["element_id"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var uidoc = app.ActiveUIDocument
            ?? throw new InvalidOperationException("No document is open.");
        var doc = uidoc.Document;

        var id = new ElementId(input["element_id"].GetInt64());
        var template = doc.GetElement(id)
            ?? throw new InvalidOperationException($"Element {id.Value} not found.");

        var typeId = template.GetTypeId();
        var catId = template.Category?.Id
            ?? throw new InvalidOperationException("Template element has no category.");

        var visibleOnly = input.TryGetValue("visible_only", out var vo) && vo.ValueKind == JsonValueKind.True;
        var collector = visibleOnly
            ? new FilteredElementCollector(doc, doc.ActiveView.Id)
            : new FilteredElementCollector(doc);

        var matches = collector
            .OfCategoryId(catId)
            .WhereElementIsNotElementType()
            .Where(e => e.GetTypeId() == typeId)
            .Select(e => e.Id)
            .ToList();

        uidoc.Selection.SetElementIds(matches);

        return JsonSerializer.Serialize(new
        {
            template_id = id.Value,
            category = template.Category?.Name,
            type_id = typeId.Value,
            selected_count = matches.Count,
            visible_only = visibleOnly,
            ids = matches.Take(50).Select(i => i.Value).ToList(),
            ids_truncated = matches.Count > 50
        });
    }
}
