using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetSelection : IRevitTool
{
    public string Name => "get_selection";

    public string Description =>
        "Returns the elements currently selected in the active Revit view. Each entry has " +
        "id, name, category, type_name, and (when applicable) host level. Call this whenever the " +
        "user refers to 'this', 'these', 'the selected', or wants you to operate on what they have picked.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>(),
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var uidoc = app.ActiveUIDocument
            ?? throw new InvalidOperationException("No document is open.");
        var doc = uidoc.Document;

        var ids = uidoc.Selection.GetElementIds();
        var elements = ids.Select(id => doc.GetElement(id))
            .Where(e => e != null)
            .Select(e => new
            {
                id = e!.Id.Value,
                name = e.Name,
                category = e.Category?.Name,
                type_name = doc.GetElement(e.GetTypeId())?.Name,
                level = e.LevelId != ElementId.InvalidElementId
                    ? doc.GetElement(e.LevelId)?.Name
                    : null
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            count = elements.Count,
            elements
        });
    }
}
