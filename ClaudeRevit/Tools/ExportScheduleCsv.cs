using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ExportScheduleCsv : IRevitTool
{
    public string Name => "export_schedule_csv";

    public string Description =>
        "Exports a ViewSchedule's data as a delimited text file (.csv or .txt). " +
        "Default delimiter is comma. If output_path is omitted, the file goes to the user's Documents folder.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["schedule_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Schedule id." }),
            ["output_path"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Absolute output path (default: Documents folder, .csv)." })
        },
        Required = ["schedule_id"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var id = new ElementId(input["schedule_id"].GetInt64());
        var schedule = doc.GetElement(id) as ViewSchedule
            ?? throw new InvalidOperationException($"Element {id.Value} is not a ViewSchedule.");

        string outPath;
        if (input.TryGetValue("output_path", out var op) && op.ValueKind == JsonValueKind.String)
            outPath = op.GetString()!;
        else
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var safe = string.Join("_", schedule.Name.Split(Path.GetInvalidFileNameChars()));
            outPath = Path.Combine(docs, $"Revit_{safe}_{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        var dir = Path.GetDirectoryName(outPath)!;
        Directory.CreateDirectory(dir);
        var name = Path.GetFileName(outPath);

        var options = new ViewScheduleExportOptions
        {
            FieldDelimiter = ",",
            TextQualifier = ExportTextQualifier.DoubleQuote
        };
        schedule.Export(dir, name, options);

        return JsonSerializer.Serialize(new
        {
            schedule = schedule.Name,
            path = outPath
        });
    }
}
