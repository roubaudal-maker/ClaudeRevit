using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class PlaceGroup : IRevitTool
{
    public string Name => "place_group";

    public string Description =>
        "Places an existing model group type at a plan-coordinate point (in feet). " +
        "Returns the new group instance id.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["group_type_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "GroupType id (returned by create_group)." }),
            ["x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Placement X (feet)." }),
            ["y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Placement Y (feet)." }),
            ["z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Placement Z (feet, optional)." })
        },
        Required = ["group_type_id", "x", "y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var typeId = new ElementId(input["group_type_id"].GetInt64());
        var groupType = doc.GetElement(typeId) as GroupType
            ?? throw new InvalidOperationException($"Element {typeId.Value} is not a GroupType.");

        var x = input["x"].GetDouble();
        var y = input["y"].GetDouble();
        var z = input.TryGetValue("z", out var zEl) ? zEl.GetDouble() : 0.0;

        var group = doc.Create.PlaceGroup(new XYZ(x, y, z), groupType);

        return JsonSerializer.Serialize(new
        {
            id = group.Id.Value,
            group_type = groupType.Name,
            position_ft = new { x, y, z }
        });
    }
}
