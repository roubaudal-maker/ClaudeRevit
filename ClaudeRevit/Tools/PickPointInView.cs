using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class PickPointInView : IRevitTool
{
    public string Name => "pick_point_in_view";

    public string Description =>
        "Asks the user to click a point in the active Revit view, then returns the picked point in feet " +
        "(world coordinates). Blocks until they click or press ESC. Use this whenever you need a precise " +
        "location from the user — e.g., 'click where you want the door' — instead of guessing coordinates. " +
        "If the user cancels, the tool returns { cancelled: true } and you should ask them what they wanted instead.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["prompt"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Optional prompt shown in Revit's status bar (e.g., 'Click the door midpoint')."
            })
        },
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var uidoc = app.ActiveUIDocument
            ?? throw new InvalidOperationException("No document is open.");

        var prompt = input.TryGetValue("prompt", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? "Click a point."
            : "Click a point.";

        XYZ point;
        try
        {
            point = uidoc.Selection.PickPoint(prompt);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return JsonSerializer.Serialize(new
            {
                cancelled = true,
                message = "User pressed ESC instead of picking a point."
            });
        }

        return JsonSerializer.Serialize(new
        {
            cancelled = false,
            point_ft = new { x = point.X, y = point.Y, z = point.Z }
        });
    }
}
