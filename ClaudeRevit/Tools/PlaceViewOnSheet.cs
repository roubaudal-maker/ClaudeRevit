using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class PlaceViewOnSheet : IRevitTool
{
    public string Name => "place_view_on_sheet";

    public string Description =>
        "Places an existing view or schedule onto a sheet at the given sheet-coordinate position (in feet from sheet origin). " +
        "Auto-detects whether the target is a regular View (uses Viewport) or a ViewSchedule (uses ScheduleSheetInstance). " +
        "A regular view can only be placed on one sheet at a time; schedules can be placed multiple times.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["sheet_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Element id of the target sheet." }),
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Element id of the view or schedule to place." }),
            ["x_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "X position on the sheet in feet (from origin)." }),
            ["y_ft"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Y position on the sheet in feet." })
        },
        Required = ["sheet_id", "view_id", "x_ft", "y_ft"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var sheetId = new ElementId(input["sheet_id"].GetInt64());
        var viewId = new ElementId(input["view_id"].GetInt64());
        var x = input["x_ft"].GetDouble();
        var y = input["y_ft"].GetDouble();

        var sheet = doc.GetElement(sheetId) as ViewSheet
            ?? throw new InvalidOperationException($"Element {sheetId.Value} is not a ViewSheet.");
        var view = doc.GetElement(viewId) as View
            ?? throw new InvalidOperationException($"Element {viewId.Value} is not a View.");

        if (view is ViewSchedule)
        {
            var instance = ScheduleSheetInstance.Create(doc, sheetId, viewId, new XYZ(x, y, 0));
            return JsonSerializer.Serialize(new
            {
                placement_id = instance.Id.Value,
                placement_type = "ScheduleSheetInstance",
                sheet = sheet.SheetNumber + " - " + sheet.Name,
                schedule = view.Name,
                position_ft = new { x, y }
            });
        }

        if (!Viewport.CanAddViewToSheet(doc, sheetId, viewId))
            throw new InvalidOperationException(
                $"View '{view.Name}' cannot be added to sheet '{sheet.Name}' " +
                "(it may already be on another sheet, or the view type isn't placeable on sheets).");

        var viewport = Viewport.Create(doc, sheetId, viewId, new XYZ(x, y, 0));
        return JsonSerializer.Serialize(new
        {
            placement_id = viewport.Id.Value,
            placement_type = "Viewport",
            sheet = sheet.SheetNumber + " - " + sheet.Name,
            view = view.Name,
            position_ft = new { x, y }
        });
    }
}
