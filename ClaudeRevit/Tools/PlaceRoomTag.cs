using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class PlaceRoomTag : IRevitTool
{
    public string Name => "place_room_tag";

    public string Description =>
        "Places a room tag on one or more rooms in a view. Rooms get their tag at their location point " +
        "(typically the center). Uses default room tag type unless tag_type_name is provided.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["room_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Room element ids to tag.",
                items = new { type = "integer" }
            }),
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id (default active view)." }),
            ["tag_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional room tag type name." })
        },
        Required = ["room_ids"]
    };

    public bool RequiresTransaction => true;

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

        FamilySymbol? tagType = null;
        if (input.TryGetValue("tag_type_name", out var tt) && tt.ValueKind == JsonValueKind.String)
        {
            var name = tt.GetString();
            tagType = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name == name);
        }

        var roomIds = input["room_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var tagged = new List<long>();
        var skipped = new List<object>();

        foreach (var id in roomIds)
        {
            var room = doc.GetElement(id) as Room;
            if (room == null)
            {
                skipped.Add(new { id = id.Value, reason = "not a Room" });
                continue;
            }

            try
            {
                var loc = room.Location as LocationPoint;
                if (loc == null)
                {
                    skipped.Add(new { id = id.Value, reason = "room has no location (unplaced)" });
                    continue;
                }
                var uv = new UV(loc.Point.X, loc.Point.Y);
                var tag = doc.Create.NewRoomTag(new LinkElementId(id), uv, view.Id);
                if (tagType != null)
                    tag.RoomTagType = tagType as RoomTagType ?? tag.RoomTagType;
                tagged.Add(tag.Id.Value);
            }
            catch (Exception ex)
            {
                skipped.Add(new { id = id.Value, reason = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            tagged_count = tagged.Count,
            skipped_count = skipped.Count,
            tag_ids = tagged,
            skipped
        });
    }
}
