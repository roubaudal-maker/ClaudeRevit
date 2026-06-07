using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class Create3DView : IRevitTool
{
    public string Name => "create_3d_view";

    public string Description =>
        "Creates a new default isometric 3D view. Optionally name it.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional view name (must be unique)." })
        },
        Required = []
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var vftId = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional)?.Id
            ?? throw new InvalidOperationException("No 3D ViewFamilyType in this document.");

        var view = View3D.CreateIsometric(doc, vftId);

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { view.Name = n.GetString(); } catch { }
        }

        return JsonSerializer.Serialize(new
        {
            id = view.Id.Value,
            type = "View3D",
            name = view.Name
        });
    }
}
