using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetActiveView : IRevitTool
{
    public string Name => "set_active_view";

    public string Description =>
        "Switches Revit's active view. Pass either view_id (preferred) or view_name (must match exactly). " +
        "Useful before placing view-specific elements (text, detail lines, dimensions).";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Target view id." }),
            ["view_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Target view name (exact match)." })
        },
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var uidoc = app.ActiveUIDocument
            ?? throw new InvalidOperationException("No document is open.");
        var doc = uidoc.Document;

        View? view = null;
        if (input.TryGetValue("view_id", out var vid) && vid.ValueKind == JsonValueKind.Number)
        {
            view = doc.GetElement(new ElementId(vid.GetInt64())) as View
                ?? throw new InvalidOperationException($"Element {vid.GetInt64()} is not a view.");
        }
        else if (input.TryGetValue("view_name", out var vn) && vn.ValueKind == JsonValueKind.String)
        {
            var name = vn.GetString();
            view = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name == name)
                ?? throw new InvalidOperationException($"View '{name}' not found.");
        }
        else
        {
            throw new InvalidOperationException("Provide either view_id or view_name.");
        }

        uidoc.ActiveView = view;

        return JsonSerializer.Serialize(new
        {
            id = view.Id.Value,
            name = view.Name,
            view_type = view.ViewType.ToString()
        });
    }
}
