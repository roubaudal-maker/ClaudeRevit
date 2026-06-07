using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class AddScheduleField : IRevitTool
{
    public string Name => "add_schedule_field";

    public string Description =>
        "Adds a column (field) to an existing ViewSchedule. The field name must match one of the schedulable " +
        "fields for the schedule's category — case-insensitive.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["schedule_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Schedule id." }),
            ["field_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Field name (e.g. 'Length', 'Area', 'Volume')." })
        },
        Required = ["schedule_id", "field_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var id = new ElementId(input["schedule_id"].GetInt64());
        var schedule = doc.GetElement(id) as ViewSchedule
            ?? throw new InvalidOperationException($"Element {id.Value} is not a ViewSchedule.");

        var fieldName = input["field_name"].GetString()!;

        var schedulable = schedule.Definition.GetSchedulableFields();
        var sf = schedulable.FirstOrDefault(f =>
            string.Equals(f.GetName(doc), fieldName, StringComparison.OrdinalIgnoreCase));

        if (sf == null)
            throw new InvalidOperationException(
                $"Field '{fieldName}' not schedulable for this schedule. Available: " +
                string.Join(", ", schedulable.Take(15).Select(f => f.GetName(doc))) + "…");

        var added = schedule.Definition.AddField(sf);

        return JsonSerializer.Serialize(new
        {
            schedule = schedule.Name,
            field_added = added.GetName(),
            field_index = added.FieldIndex
        });
    }
}
