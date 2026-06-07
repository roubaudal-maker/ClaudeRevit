using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateSpotElevation : IRevitTool
{
    public string Name => "create_spot_elevation";

    public string Description =>
        "Adds a spot-elevation annotation pointing to an element's top in the active view. " +
        "The leader runs from the element to the text at (leader_end_x, leader_end_y) via a bend at (bend_x, bend_y).";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Element to dimension." }),
            ["bend_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader bend X (feet)." }),
            ["bend_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader bend Y (feet)." }),
            ["leader_end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader/text end X (feet)." }),
            ["leader_end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader/text end Y (feet)." })
        },
        Required = ["element_id", "bend_x", "bend_y", "leader_end_x", "leader_end_y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");
        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var id = new ElementId(input["element_id"].GetInt64());
        var element = doc.GetElement(id)
            ?? throw new InvalidOperationException($"Element {id.Value} not found.");

        var bbox = element.get_BoundingBox(view) ?? element.get_BoundingBox(null)
            ?? throw new InvalidOperationException("Element has no bounding box.");

        var centerX = (bbox.Min.X + bbox.Max.X) / 2;
        var centerY = (bbox.Min.Y + bbox.Max.Y) / 2;
        var topZ = bbox.Max.Z;
        var origin = new XYZ(centerX, centerY, topZ);

        var bend = new XYZ(input["bend_x"].GetDouble(), input["bend_y"].GetDouble(), topZ);
        var end = new XYZ(input["leader_end_x"].GetDouble(), input["leader_end_y"].GetDouble(), topZ);

        var reference = new Reference(element);
        var spot = doc.Create.NewSpotElevation(view, reference, origin, bend, end, origin, true);

        return JsonSerializer.Serialize(new
        {
            id = spot.Id.Value,
            type = "SpotElevation",
            view = view.Name,
            element_id = id.Value,
            elevation_ft = topZ
        });
    }
}
