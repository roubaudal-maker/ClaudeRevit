using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ExportDwg : IRevitTool
{
    public string Name => "export_dwg";

    public string Description =>
        "Exports one or more views/sheets to AutoCAD DWG files. Each view becomes its own .dwg in the " +
        "output folder. Uses Revit's default DWG export settings.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Views or sheets to export.",
                items = new { type = "integer" }
            }),
            ["output_dir"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Output directory (default: Documents)." }),
            ["file_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Base file name (default: 'Revit_dwg_<timestamp>'). Revit appends view name." })
        },
        Required = ["view_ids"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var viewIds = input["view_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var outDir = input.TryGetValue("output_dir", out var od) && od.ValueKind == JsonValueKind.String
            ? od.GetString()!
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Directory.CreateDirectory(outDir);

        var baseName = input.TryGetValue("file_name", out var fn) && fn.ValueKind == JsonValueKind.String
            ? fn.GetString()!
            : $"Revit_dwg_{DateTime.Now:yyyyMMdd-HHmmss}";

        var options = new DWGExportOptions();

        doc.Export(outDir, baseName, viewIds, options);

        return JsonSerializer.Serialize(new
        {
            view_count = viewIds.Count,
            output_dir = outDir,
            base_name = baseName
        });
    }
}
