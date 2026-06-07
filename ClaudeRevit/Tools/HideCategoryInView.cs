using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class HideCategoryInView : IRevitTool
{
    public string Name => "hide_category_in_view";

    public string Description =>
        "Hides or shows an entire category in a view (defaults to active view). " +
        "Set 'hide' to true to hide, false to make visible again.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["category"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Category to toggle, e.g. 'Walls', 'Doors', 'Grids'."
            }),
            ["hide"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "true = hide (default), false = show." }),
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id (default active view)." })
        },
        Required = ["category"]
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

        var category = input["category"].GetString()!;
        if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", true, out var bic))
            throw new InvalidOperationException($"Unknown category '{category}'.");

        var catId = new ElementId(bic);
        var hide = !input.TryGetValue("hide", out var h) || h.ValueKind != JsonValueKind.False;

        if (!view.CanCategoryBeHidden(catId))
            throw new InvalidOperationException(
                $"Category '{category}' cannot be hidden in '{view.Name}'.");

        view.SetCategoryHidden(catId, hide);

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            category,
            hidden = hide
        });
    }
}
