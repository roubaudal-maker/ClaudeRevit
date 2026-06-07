using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ChangeElementType : IRevitTool
{
    public string Name => "change_element_type";

    public string Description =>
        "Swaps the type of one or more elements to a new type id. The new type must be compatible with the " +
        "element's category — e.g. you can swap a Wall to a different WallType, but not to a FloorType. " +
        "Use list_family_types to find the right new_type_id.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Elements whose type should change.",
                items = new { type = "integer" }
            }),
            ["new_type_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "New ElementType id." })
        },
        Required = ["element_ids", "new_type_id"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var newTypeId = new ElementId(input["new_type_id"].GetInt64());
        var newType = doc.GetElement(newTypeId) as ElementType
            ?? throw new InvalidOperationException($"Element {newTypeId.Value} is not an ElementType.");

        var elementIds = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var changed = new List<long>();
        var skipped = new List<object>();

        foreach (var id in elementIds)
        {
            var el = doc.GetElement(id);
            if (el == null) { skipped.Add(new { id = id.Value, reason = "not found" }); continue; }
            try { el.ChangeTypeId(newTypeId); changed.Add(id.Value); }
            catch (Exception ex) { skipped.Add(new { id = id.Value, reason = ex.Message }); }
        }

        return JsonSerializer.Serialize(new
        {
            new_type = newType.Name,
            changed_count = changed.Count,
            skipped_count = skipped.Count,
            skipped
        });
    }
}
