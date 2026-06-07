using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SaveDocument : IRevitTool
{
    public string Name => "save_document";

    public string Description =>
        "Saves the active Revit document. If 'save_as_path' is provided, saves to that path (Save As); " +
        "otherwise saves to the document's current path. Cannot be called inside an open transaction.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["save_as_path"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional absolute path for Save As." })
        },
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        if (input.TryGetValue("save_as_path", out var sa) && sa.ValueKind == JsonValueKind.String)
        {
            var path = sa.GetString()!;
            doc.SaveAs(path);
            return JsonSerializer.Serialize(new { saved_as = path });
        }

        if (string.IsNullOrEmpty(doc.PathName))
            throw new InvalidOperationException(
                "Document has never been saved — provide save_as_path for the first save.");

        doc.Save();
        return JsonSerializer.Serialize(new { saved = true, path = doc.PathName });
    }
}
