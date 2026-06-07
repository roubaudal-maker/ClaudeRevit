using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateDraftingView : IRevitTool
{
    public string Name => "create_drafting_view";

    public string Description =>
        "Creates a new drafting view (a 2D detail view not tied to model geometry). " +
        "Useful for typical details, legends, or notes. Optionally name it and set a scale.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional view name." }),
            ["scale"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Scale denominator (e.g. 10 for 1:10). Default 100.", minimum = 1 })
        },
        Required = []
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var vftId = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting)?.Id
            ?? throw new InvalidOperationException("No Drafting ViewFamilyType in this document.");

        var view = ViewDrafting.Create(doc, vftId);

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { view.Name = n.GetString(); } catch { }
        }
        if (input.TryGetValue("scale", out var s) && s.ValueKind == JsonValueKind.Number)
        {
            try { view.Scale = s.GetInt32(); } catch { }
        }

        return JsonSerializer.Serialize(new
        {
            id = view.Id.Value,
            type = "ViewDrafting",
            name = view.Name,
            scale = view.Scale
        });
    }
}
