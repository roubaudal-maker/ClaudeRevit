using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateCallout : IRevitTool
{
    public string Name => "create_callout";

    public string Description =>
        "Creates a detail callout in a parent view, defined by two diagonal corner points (in feet). " +
        "Returns the new detail view's id. If parent_view_id is omitted, the active view is used.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["parent_view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Parent view id (default active view)." }),
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Callout box corner X (feet)." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Callout box corner Y (feet)." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Opposite corner X (feet)." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Opposite corner Y (feet)." }),
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional callout view name." })
        },
        Required = ["start_x", "start_y", "end_x", "end_y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        View parent;
        if (input.TryGetValue("parent_view_id", out var pv) && pv.ValueKind == JsonValueKind.Number)
            parent = doc.GetElement(new ElementId(pv.GetInt64())) as View
                ?? throw new InvalidOperationException("parent_view_id is not a view.");
        else
            parent = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var p1 = new XYZ(input["start_x"].GetDouble(), input["start_y"].GetDouble(), 0);
        var p2 = new XYZ(input["end_x"].GetDouble(), input["end_y"].GetDouble(), 0);

        var detailTypeId = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.ViewFamily == ViewFamily.Detail)?.Id
            ?? throw new InvalidOperationException("No Detail ViewFamilyType in this document.");

        var callout = ViewSection.CreateCallout(doc, parent.Id, detailTypeId, p1, p2);

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { callout.Name = n.GetString(); } catch { }
        }

        return JsonSerializer.Serialize(new
        {
            id = callout.Id.Value,
            type = "Callout",
            name = callout.Name,
            parent_view = parent.Name
        });
    }
}
