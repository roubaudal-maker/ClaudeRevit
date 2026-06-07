using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateTextWithLeader : IRevitTool
{
    public string Name => "create_text_with_leader";

    public string Description =>
        "Adds a text note with a leader line in the active view. The leader points FROM the text " +
        "(text_x, text_y) TO the target (leader_end_x, leader_end_y). Useful for callouts. " +
        "Leader direction: 'left' or 'right' — choose based on which side of the text the leader exits.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The note's text." }),
            ["text_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Text position X (feet)." }),
            ["text_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Text position Y (feet)." }),
            ["leader_end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader end X (where the arrow points)." }),
            ["leader_end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Leader end Y." }),
            ["leader_direction"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Which side of the text the leader exits.",
                @enum = new[] { "left", "right" }
            })
        },
        Required = ["text", "text_x", "text_y", "leader_end_x", "leader_end_y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");
        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");

        var text = input["text"].GetString()!;
        var tx = input["text_x"].GetDouble();
        var ty = input["text_y"].GetDouble();
        var lx = input["leader_end_x"].GetDouble();
        var ly = input["leader_end_y"].GetDouble();

        var direction = input.TryGetValue("leader_direction", out var ld) && ld.ValueKind == JsonValueKind.String
            ? ld.GetString()!.ToLowerInvariant()
            : (lx < tx ? "left" : "right");

        var defaultId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        var type = (defaultId != ElementId.InvalidElementId
            ? doc.GetElement(defaultId) as TextNoteType
            : null)
            ?? new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault()
            ?? throw new InvalidOperationException("No TextNoteType available.");

        var note = TextNote.Create(doc, view.Id, new XYZ(tx, ty, 0), text, type.Id);

        var leaderType = direction == "left" ? TextNoteLeaderTypes.TNLT_STRAIGHT_L : TextNoteLeaderTypes.TNLT_STRAIGHT_R;
        var leader = note.AddLeader(leaderType);
        leader.End = new XYZ(lx, ly, 0);

        return JsonSerializer.Serialize(new
        {
            id = note.Id.Value,
            type = "TextNote",
            view = view.Name,
            text_position_ft = new { x = tx, y = ty },
            leader_end_ft = new { x = lx, y = ly },
            leader_direction = direction
        });
    }
}
