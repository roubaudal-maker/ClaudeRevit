using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class DeleteElements : IRevitTool
{
    public string Name => "delete_elements";

    public string Description =>
        "Deletes the specified elements (by id) from the active document. Note that deleting a host element " +
        "(e.g., a wall) also deletes anything hosted on it (doors, windows). Always confirm with the user " +
        "before deleting more than a few elements.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 1,
                description = "Element ids to delete.",
                items = new { type = "integer" }
            })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var arr = input["element_ids"];
        if (arr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("'element_ids' must be an array of integers.");

        var ids = arr.EnumerateArray()
            .Select(e => new ElementId(e.GetInt64()))
            .ToList();

        var missing = ids.Where(id => doc.GetElement(id) == null).Select(id => id.Value).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"These ids do not exist in the document: {string.Join(", ", missing)}");

        var deleted = doc.Delete(ids);

        return JsonSerializer.Serialize(new
        {
            requested = ids.Count,
            deleted_count = deleted.Count,
            deleted_ids = deleted.Select(id => id.Value).ToList()
        });
    }
}
