using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateDependentView : IRevitTool
{
    public string Name => "create_dependent_view";

    public string Description =>
        "Creates a dependent view from a source view (e.g. for splitting a large plan across multiple sheets). " +
        "Dependent views share view-template and visibility settings with the parent; crop regions are independent.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["source_view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Source view id." }),
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional name for the dependent view." })
        },
        Required = ["source_view_id"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var srcId = new ElementId(input["source_view_id"].GetInt64());
        var src = doc.GetElement(srcId) as View
            ?? throw new InvalidOperationException("source_view_id is not a view.");

        if (!src.CanViewBeDuplicated(ViewDuplicateOption.AsDependent))
            throw new InvalidOperationException(
                $"View '{src.Name}' does not support dependent duplication.");

        var newId = src.Duplicate(ViewDuplicateOption.AsDependent);
        var dep = doc.GetElement(newId) as View
            ?? throw new InvalidOperationException("Duplicate failed.");

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { dep.Name = n.GetString(); } catch { }
        }

        return JsonSerializer.Serialize(new
        {
            id = dep.Id.Value,
            name = dep.Name,
            parent_view = src.Name
        });
    }
}
