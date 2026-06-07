using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class UnjoinGeometry : IRevitTool
{
    public string Name => "unjoin_geometry";

    public string Description =>
        "Unjoins the geometry of two previously joined elements.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_a_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "First element id." }),
            ["element_b_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Second element id." })
        },
        Required = ["element_a_id", "element_b_id"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var a = doc.GetElement(new ElementId(input["element_a_id"].GetInt64()))
            ?? throw new InvalidOperationException("element_a_id not found.");
        var b = doc.GetElement(new ElementId(input["element_b_id"].GetInt64()))
            ?? throw new InvalidOperationException("element_b_id not found.");

        if (!JoinGeometryUtils.AreElementsJoined(doc, a, b))
            return JsonSerializer.Serialize(new { not_joined = true, a_id = a.Id.Value, b_id = b.Id.Value });

        JoinGeometryUtils.UnjoinGeometry(doc, a, b);

        return JsonSerializer.Serialize(new
        {
            unjoined = true,
            a_id = a.Id.Value,
            b_id = b.Id.Value
        });
    }
}
