using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateSpotCoordinate : IRevitTool
{
    public string Name => "create_spot_coordinate";

    public string Description =>
        "Adds a spot-coordinate annotation pointing to an element in the active view, showing its X/Y " +
        "world coordinates. Leader runs from element via a bend point to the text end.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Element to annotate." }),
            ["bend_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader bend X (feet)." }),
            ["bend_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader bend Y (feet)." }),
            ["leader_end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader end X (feet)." }),
            ["leader_end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader end Y (feet)." })
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
        var z = bbox.Max.Z;
        var origin = new XYZ(centerX, centerY, z);

        var bend = new XYZ(input["bend_x"].GetDouble(), input["bend_y"].GetDouble(), z);
        var end = new XYZ(input["leader_end_x"].GetDouble(), input["leader_end_y"].GetDouble(), z);

        var reference = new Reference(element);
        var spot = doc.Create.NewSpotCoordinate(view, reference, origin, bend, end, origin, true);

        return JsonSerializer.Serialize(new
        {
            id = spot.Id.Value,
            type = "SpotCoordinate",
            view = view.Name,
            element_id = id.Value,
            x_ft = centerX,
            y_ft = centerY
        });
    }
}
