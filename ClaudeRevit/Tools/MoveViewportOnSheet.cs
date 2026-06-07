using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class MoveViewportOnSheet : IRevitTool
{
    public string Name => "move_viewport_on_sheet";

    public string Description =>
        "Moves a viewport (a placed view on a sheet) to a new center position on the sheet, in feet from the sheet origin.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["viewport_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Viewport id (from get_sheet_views)." }),
            ["x_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "New center X on the sheet (feet)." }),
            ["y_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "New center Y on the sheet (feet)." })
        },
        Required = ["viewport_id", "x_ft", "y_ft"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var vpId = new ElementId(input["viewport_id"].GetInt64());
        var vp = doc.GetElement(vpId) as Viewport
            ?? throw new InvalidOperationException($"Element {vpId.Value} is not a Viewport.");

        var x = input["x_ft"].GetDouble();
        var y = input["y_ft"].GetDouble();
        vp.SetBoxCenter(new XYZ(x, y, 0));

        return JsonSerializer.Serialize(new
        {
            viewport_id = vpId.Value,
            new_center_ft = new { x, y }
        });
    }
}
