using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateDetailLine : IRevitTool
{
    public string Name => "create_detail_line";

    public string Description =>
        "Creates a straight detail line in the active view between two plan-coordinate points (in feet). " +
        "Detail lines are view-specific annotations — they don't appear in 3D or other views.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start X (feet)." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start Y (feet)." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End X (feet)." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End Y (feet)." })
        },
        Required = ["start_x", "start_y", "end_x", "end_y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");
        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var start = new XYZ(input["start_x"].GetDouble(), input["start_y"].GetDouble(), 0);
        var end = new XYZ(input["end_x"].GetDouble(), input["end_y"].GetDouble(), 0);
        if (start.IsAlmostEqualTo(end))
            throw new InvalidOperationException("Detail line has zero length.");

        var line = Line.CreateBound(start, end);
        var detailLine = doc.Create.NewDetailCurve(view, line);

        return JsonSerializer.Serialize(new
        {
            id = detailLine.Id.Value,
            type = "DetailLine",
            view = view.Name,
            length_ft = line.Length
        });
    }
}
