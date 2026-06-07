using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class JoinGeometry : IRevitTool
{
    public string Name => "join_geometry";

    public string Description =>
        "Joins the geometry of two elements (walls + floors + roofs + columns). Joined elements share " +
        "boundaries and their cleanups behave together at intersections.";

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

        if (JoinGeometryUtils.AreElementsJoined(doc, a, b))
            return JsonSerializer.Serialize(new { already_joined = true, a_id = a.Id.Value, b_id = b.Id.Value });

        JoinGeometryUtils.JoinGeometry(doc, a, b);

        return JsonSerializer.Serialize(new
        {
            joined = true,
            a_id = a.Id.Value,
            b_id = b.Id.Value
        });
    }
}
