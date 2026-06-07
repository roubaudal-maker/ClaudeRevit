using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class TagElements : IRevitTool
{
    public string Name => "tag_elements";

    public string Description =>
        "Adds tags to one or more elements in the active view (or a specified view). Tags are placed at each " +
        "element's location point or curve midpoint. Uses the loaded tag family for each element's category. " +
        "Set 'leader' to true if the user wants leader lines.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 1,
                description = "Elements to tag.",
                items = new { type = "integer" }
            }),
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id. Defaults to the active view." }),
            ["leader"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "Include leader line (default false)." })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        View view;
        if (input.TryGetValue("view_id", out var vid) && vid.ValueKind == JsonValueKind.Number)
        {
            view = doc.GetElement(new ElementId(vid.GetInt64())) as View
                ?? throw new InvalidOperationException($"Element {vid.GetInt64()} is not a view.");
        }
        else
        {
            view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");
        }

        var leader = input.TryGetValue("leader", out var l) && l.ValueKind == JsonValueKind.True;
        var elementIds = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64()))
            .ToList();

        var tagged = new List<long>();
        var skipped = new List<object>();

        foreach (var id in elementIds)
        {
            var element = doc.GetElement(id);
            if (element == null)
            {
                skipped.Add(new { id = id.Value, reason = "not found" });
                continue;
            }

            try
            {
                var position = GetTagPosition(element);
                var reference = new Reference(element);
                var tag = IndependentTag.Create(
                    doc, view.Id, reference,
                    leader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal,
                    position);
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

    private static XYZ GetTagPosition(Element element)
    {
        if (element.Location is LocationPoint lp) return lp.Point;
        if (element.Location is LocationCurve lc)
        {
            var c = lc.Curve;
            return c.Evaluate(0.5, true);
        }
        var bbox = element.get_BoundingBox(null);
        if (bbox != null) return (bbox.Min + bbox.Max) / 2;
        return XYZ.Zero;
    }
}
