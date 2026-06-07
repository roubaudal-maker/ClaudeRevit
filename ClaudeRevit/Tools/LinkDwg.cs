using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class LinkDwg : IRevitTool
{
    public string Name => "link_dwg";

    public string Description =>
        "Links a DWG file into the active view (or a specified view). The DWG is linked, not imported — " +
        "edits in the source file can be picked up by reload_link.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["file_path"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Absolute path to .dwg." }),
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional target view (default active view)." }),
            ["current_view_only"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "If true, link is only visible in the target view (default false)." })
        },
        Required = ["file_path"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var path = input["file_path"].GetString()
            ?? throw new InvalidOperationException("file_path is required.");
        if (!File.Exists(path))
            throw new InvalidOperationException($"File not found: {path}");

        View view;
        if (input.TryGetValue("view_id", out var vid) && vid.ValueKind == JsonValueKind.Number)
            view = doc.GetElement(new ElementId(vid.GetInt64())) as View
                ?? throw new InvalidOperationException("view_id is not a view.");
        else
            view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var options = new DWGImportOptions
        {
            Placement = ImportPlacement.Origin,
            OrientToView = true,
            CustomScale = 1.0,
            Unit = ImportUnit.Default,
            ColorMode = ImportColorMode.Preserved,
            ThisViewOnly = input.TryGetValue("current_view_only", out var cvo) && cvo.ValueKind == JsonValueKind.True
        };

        var linked = doc.Link(path, options, view, out ElementId linkId);
        if (!linked)
            throw new InvalidOperationException("Revit refused to link the DWG. Check file path and version.");

        return JsonSerializer.Serialize(new
        {
            link_id = linkId.Value,
            type = "DWG Link",
            file = Path.GetFileName(path),
            view = view.Name,
            current_view_only = options.ThisViewOnly
        });
    }
}
