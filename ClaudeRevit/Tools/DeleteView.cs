using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class DeleteView : IRevitTool
{
    public string Name => "delete_view";

    public string Description =>
        "Deletes one or more views (or sheets). Cannot delete the active view, the last view of a doc, or " +
        "views that are templates currently applied to other views — Revit will raise an error in those cases.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Views or sheets to delete.",
                items = new { type = "integer" }
            })
        },
        Required = ["view_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["view_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var deleted = new List<long>();
        var skipped = new List<object>();

        foreach (var id in ids)
        {
            var v = doc.GetElement(id);
            if (v == null) { skipped.Add(new { id = id.Value, reason = "not found" }); continue; }
            if (v is not View) { skipped.Add(new { id = id.Value, reason = "not a view" }); continue; }
            try { doc.Delete(id); deleted.Add(id.Value); }
            catch (Exception ex) { skipped.Add(new { id = id.Value, reason = ex.Message }); }
        }

        return JsonSerializer.Serialize(new
        {
            deleted_count = deleted.Count,
            deleted,
            skipped_count = skipped.Count,
            skipped
        });
    }
}
