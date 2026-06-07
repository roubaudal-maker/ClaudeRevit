using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ExportImage : IRevitTool
{
    public string Name => "export_image";

    public string Description =>
        "Exports a view (or the active view) to a PNG image file. If output_path is omitted, " +
        "the image is saved to the user's Pictures folder with a timestamp filename. Returns the saved path.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "View to export (defaults to active view)." }),
            ["output_path"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Absolute output path (default: Pictures folder, timestamped)." }),
            ["pixel_size"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Largest dimension in pixels (default 1600).", minimum = 256, maximum = 8192 })
        },
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        View view;
        if (input.TryGetValue("view_id", out var vid) && vid.ValueKind == JsonValueKind.Number)
            view = doc.GetElement(new ElementId(vid.GetInt64())) as View
                ?? throw new InvalidOperationException("view_id is not a view.");
        else
            view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var pixels = input.TryGetValue("pixel_size", out var ps) ? ps.GetInt32() : 1600;
        if (pixels < 256 || pixels > 8192) pixels = 1600;

        string outPath;
        if (input.TryGetValue("output_path", out var op) && op.ValueKind == JsonValueKind.String)
        {
            outPath = op.GetString()!;
        }
        else
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var safe = string.Join("_", view.Name.Split(Path.GetInvalidFileNameChars()));
            outPath = Path.Combine(pictures, $"Revit_{safe}_{DateTime.Now:yyyyMMdd-HHmmss}.png");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = outPath,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = pixels,
            ImageResolution = ImageResolution.DPI_150,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            FitDirection = FitDirectionType.Horizontal
        };
        options.SetViewsAndSheets(new List<ElementId> { view.Id });

        doc.ExportImage(options);

        // Revit may add the view name to the filename if exporting multiple — find the actual file
        var actualPath = File.Exists(outPath) ? outPath : FindNearbyExport(outPath);

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            path = actualPath ?? outPath,
            pixel_size = pixels
        });
    }

    private static string? FindNearbyExport(string requestedPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(requestedPath);
            if (dir == null || !Directory.Exists(dir)) return null;
            var prefix = Path.GetFileNameWithoutExtension(requestedPath);
            var match = Directory.GetFiles(dir, $"{prefix}*.png");
            return match.Length > 0 ? match[0] : null;
        }
        catch { return null; }
    }
}
