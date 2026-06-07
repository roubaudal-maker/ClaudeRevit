using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateTextNote : IRevitTool
{
    public string Name => "create_text_note";

    public string Description =>
        "Adds a text annotation in the active view at a plan-coordinate point (in feet). " +
        "Uses the document's default text-note type unless 'text_type_name' is specified.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Position X (feet)." }),
            ["y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Position Y (feet)." }),
            ["text"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The note's text content." }),
            ["text_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional text-note type name." })
        },
        Required = ["x", "y", "text"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");
        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var x = input["x"].GetDouble();
        var y = input["y"].GetDouble();
        var text = input["text"].GetString()!;

        TextNoteType type;
        if (input.TryGetValue("text_type_name", out var ttn) && ttn.ValueKind == JsonValueKind.String)
        {
            var name = ttn.GetString();
            type = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>()
                .FirstOrDefault(t => t.Name == name)
                ?? throw new InvalidOperationException($"Text note type '{name}' not found.");
        }
        else
        {
            var defaultId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
            type = (defaultId != ElementId.InvalidElementId
                ? doc.GetElement(defaultId) as TextNoteType
                : null)
                ?? new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault()
                ?? throw new InvalidOperationException("No TextNoteType available in document.");
        }

        var note = TextNote.Create(doc, view.Id, new XYZ(x, y, 0), text, type.Id);

        return JsonSerializer.Serialize(new
        {
            id = note.Id.Value,
            type = "TextNote",
            view = view.Name,
            position_ft = new { x, y },
            text_type = type.Name
        });
    }
}
