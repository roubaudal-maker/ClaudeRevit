using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ExportPdf : IRevitTool
{
    public string Name => "export_pdf";

    public string Description =>
        "Exports one or more views/sheets to a PDF file. If 'combine' is true (default), all views go into " +
        "one multi-page PDF; otherwise each view becomes its own PDF in the output folder. " +
        "Default output is the user's Documents folder.";

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
            ["file_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Output file name without extension (default: 'Revit_export_<timestamp>')." }),
            ["combine"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "Combine into a single PDF (default true)." })
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
            : $"Revit_export_{DateTime.Now:yyyyMMdd-HHmmss}";

        var combine = !input.TryGetValue("combine", out var c) || c.ValueKind != JsonValueKind.False;

        var options = new PDFExportOptions
        {
            FileName = baseName,
            Combine = combine,
            ExportQuality = PDFExportQualityType.DPI300,
            PaperFormat = ExportPaperFormat.Default,
            PaperOrientation = PageOrientationType.Auto,
            ZoomType = ZoomType.Zoom,
            ZoomPercentage = 100
        };

        doc.Export(outDir, viewIds, options);

        return JsonSerializer.Serialize(new
        {
            view_count = viewIds.Count,
            output_dir = outDir,
            base_name = baseName,
            combined = combine
        });
    }
}
