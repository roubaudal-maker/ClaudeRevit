using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class DuplicateSheet : IRevitTool
{
    public string Name => "duplicate_sheet";

    public string Description =>
        "Duplicates a sheet (without its placed views/schedules — those don't transfer in Revit). " +
        "Optionally rename and renumber the new sheet.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["sheet_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Source sheet id." }),
            ["new_number"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional new sheet number." }),
            ["new_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional new sheet name." })
        },
        Required = ["sheet_id"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var sourceId = new ElementId(input["sheet_id"].GetInt64());
        var source = doc.GetElement(sourceId) as ViewSheet
            ?? throw new InvalidOperationException($"Element {sourceId.Value} is not a ViewSheet.");

        if (!source.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
            throw new InvalidOperationException($"Sheet '{source.Name}' cannot be duplicated.");

        var newId = source.Duplicate(ViewDuplicateOption.Duplicate);
        var newSheet = doc.GetElement(newId) as ViewSheet
            ?? throw new InvalidOperationException("Duplicate succeeded but couldn't fetch new sheet.");

        if (input.TryGetValue("new_number", out var nn) && nn.ValueKind == JsonValueKind.String)
        {
            try { newSheet.SheetNumber = nn.GetString(); } catch { }
        }
        if (input.TryGetValue("new_name", out var nm) && nm.ValueKind == JsonValueKind.String)
        {
            try { newSheet.Name = nm.GetString(); } catch { }
        }

        return JsonSerializer.Serialize(new
        {
            id = newSheet.Id.Value,
            type = "Sheet",
            number = newSheet.SheetNumber,
            name = newSheet.Name,
            duplicated_from = source.SheetNumber + " - " + source.Name
        });
    }
}
