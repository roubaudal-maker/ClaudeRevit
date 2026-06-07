using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetViewScale : IRevitTool
{
    public string Name => "set_view_scale";

    public string Description =>
        "Sets the scale of a view (or the active view if view_id is omitted). " +
        "Scale is the denominator: pass 50 for 1:50, 100 for 1:100, etc.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id (defaults to active view)." }),
            ["scale"] = JsonSerializer.SerializeToElement(new
            {
                type = "integer",
                description = "Scale denominator (e.g. 50 means 1:50). Must be 1 or greater.",
                minimum = 1
            })
        },
        Required = ["scale"]
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

        var scale = input["scale"].GetInt32();
        if (scale < 1) throw new InvalidOperationException("scale must be 1 or greater.");

        view.Scale = scale;

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            new_scale = view.Scale
        });
    }
}
