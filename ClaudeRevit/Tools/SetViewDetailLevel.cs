using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetViewDetailLevel : IRevitTool
{
    public string Name => "set_view_detail_level";

    public string Description =>
        "Sets the detail level of a view to 'coarse', 'medium', or 'fine'. " +
        "Defaults to the active view if view_id is omitted.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id." }),
            ["level"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Detail level.",
                @enum = new[] { "coarse", "medium", "fine" }
            })
        },
        Required = ["level"]
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

        var levelStr = input["level"].GetString()!.ToLowerInvariant();
        var level = levelStr switch
        {
            "coarse" => ViewDetailLevel.Coarse,
            "medium" => ViewDetailLevel.Medium,
            "fine" => ViewDetailLevel.Fine,
            _ => throw new InvalidOperationException($"Unknown detail level '{levelStr}'.")
        };

        view.DetailLevel = level;

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            detail_level = view.DetailLevel.ToString()
        });
    }
}
