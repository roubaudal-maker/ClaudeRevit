using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetSheetViews : IRevitTool
{
    public string Name => "get_sheet_views";

    public string Description =>
        "Lists all views and schedules placed on a sheet, with their viewport/instance ids and positions. " +
        "Useful before rearranging a sheet or before placing more content.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["sheet_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Sheet id." })
        },
        Required = ["sheet_id"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var sheetId = new ElementId(input["sheet_id"].GetInt64());
        var sheet = doc.GetElement(sheetId) as ViewSheet
            ?? throw new InvalidOperationException($"Element {sheetId.Value} is not a ViewSheet.");

        var viewports = new FilteredElementCollector(doc)
            .OfClass(typeof(Viewport)).Cast<Viewport>()
            .Where(v => v.SheetId == sheetId)
            .Select(vp =>
            {
                var view = doc.GetElement(vp.ViewId) as View;
                var center = vp.GetBoxCenter();
                return new
                {
                    viewport_id = vp.Id.Value,
                    view_id = vp.ViewId.Value,
                    view_name = view?.Name,
                    view_type = view?.ViewType.ToString(),
                    center_ft = new { x = center.X, y = center.Y }
                };
            }).ToList();

        var scheduleInstances = new FilteredElementCollector(doc)
            .OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>()
            .Where(s => s.OwnerViewId == sheetId)
            .Select(si =>
            {
                var schedule = doc.GetElement(si.ScheduleId) as ViewSchedule;
                return new
                {
                    instance_id = si.Id.Value,
                    schedule_id = si.ScheduleId.Value,
                    schedule_name = schedule?.Name,
                    point_ft = new { x = si.Point.X, y = si.Point.Y }
                };
            }).ToList();

        return JsonSerializer.Serialize(new
        {
            sheet = sheet.SheetNumber + " - " + sheet.Name,
            viewport_count = viewports.Count,
            viewports,
            schedule_count = scheduleInstances.Count,
            schedules = scheduleInstances
        });
    }
}
