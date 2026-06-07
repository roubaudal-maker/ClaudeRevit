using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class IsolateElementsInView : IRevitTool
{
    public string Name => "isolate_elements_in_view";

    public string Description =>
        "Temporarily isolates elements in the active view (other elements are hidden). The user can clear " +
        "isolation via Revit's temporary view-mode toggle. Useful for focused inspection or before screenshots.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Elements to isolate.",
                items = new { type = "integer" }
            }),
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id (defaults to active view)." })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        View view;
        if (input.TryGetValue("view_id", out var vid) && vid.ValueKind == JsonValueKind.Number)
            view = doc.GetElement(new ElementId(vid.GetInt64())) as View
                ?? throw new InvalidOperationException("view_id is not a view.");
        else
            view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        view.IsolateElementsTemporary(ids);

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            isolated_count = ids.Count,
            note = "Temporary isolation — toggle off via Revit's view-mode controls."
        });
    }
}
