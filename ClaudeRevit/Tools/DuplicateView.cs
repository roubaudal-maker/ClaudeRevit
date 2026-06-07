using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class DuplicateView : IRevitTool
{
    public string Name => "duplicate_view";

    public string Description =>
        "Duplicates a view. mode can be 'duplicate' (no annotations), 'with_detailing' (includes annotations), " +
        "or 'as_dependent' (dependent view sharing extents). Returns the new view's id.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["source_view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Source view id." }),
            ["mode"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Duplicate mode.",
                @enum = new[] { "duplicate", "with_detailing", "as_dependent" }
            }),
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional name for the new view." })
        },
        Required = ["source_view_id"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var sourceId = new ElementId(input["source_view_id"].GetInt64());
        var source = doc.GetElement(sourceId) as View
            ?? throw new InvalidOperationException($"Element {sourceId.Value} is not a view.");

        var mode = input.TryGetValue("mode", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()!.ToLowerInvariant()
            : "duplicate";

        var option = mode switch
        {
            "with_detailing" => ViewDuplicateOption.WithDetailing,
            "as_dependent" => ViewDuplicateOption.AsDependent,
            _ => ViewDuplicateOption.Duplicate
        };

        if (!source.CanViewBeDuplicated(option))
            throw new InvalidOperationException(
                $"View '{source.Name}' cannot be duplicated with mode '{mode}'.");

        var newId = source.Duplicate(option);
        var newView = doc.GetElement(newId) as View
            ?? throw new InvalidOperationException("Duplicate succeeded but couldn't fetch new view.");

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { newView.Name = n.GetString(); } catch { }
        }

        return JsonSerializer.Serialize(new
        {
            id = newView.Id.Value,
            type = newView.GetType().Name,
            name = newView.Name,
            duplicated_from = source.Name
        });
    }
}
