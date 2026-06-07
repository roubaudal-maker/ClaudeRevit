using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class LinkRevitModel : IRevitTool
{
    public string Name => "link_revit_model";

    public string Description =>
        "Links a Revit (.rvt) model into the current document. Returns the link type and instance ids. " +
        "Use list_links to verify it loaded; use reload_link to refresh later.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["file_path"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Absolute path to the .rvt file." })
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

        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(path);
        var options = new RevitLinkOptions(false);

        var loadResult = RevitLinkType.Create(doc, modelPath, options);
        if (loadResult.LoadResult != LinkLoadResultType.LinkLoaded)
            throw new InvalidOperationException(
                $"Revit refused to link the model. LoadResult: {loadResult.LoadResult}");

        var linkInstance = RevitLinkInstance.Create(doc, loadResult.ElementId);

        return JsonSerializer.Serialize(new
        {
            link_type_id = loadResult.ElementId.Value,
            link_instance_id = linkInstance.Id.Value,
            file = Path.GetFileName(path),
            load_result = loadResult.LoadResult.ToString()
        });
    }
}
