using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class TagAllInView : IRevitTool
{
    public string Name => "tag_all_in_view";

    public string Description =>
        "Auto-tags every element of the given category that is visible in a view (defaults to active view). " +
        "Skips elements that are already tagged in that view. Tag positions are picked at each element's " +
        "location point or curve midpoint.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["category"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Category to tag, e.g. 'Doors', 'Windows', 'Walls', 'StructuralColumns'."
            }),
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id (default active view)." }),
            ["leader"] = JsonSerializer.SerializeToElement(new { type = "boolean", description = "Include leader line (default false)." })
        },
        Required = ["category"]
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

        var category = input["category"].GetString()!;
        if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", true, out var bic))
            throw new InvalidOperationException($"Unknown category '{category}'.");

        var leader = input.TryGetValue("leader", out var l) && l.ValueKind == JsonValueKind.True;

        var alreadyTagged = new HashSet<long>(
            new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
                .SelectMany(t => t.GetTaggedLocalElementIds().Select(id => id.Value)));

        var elements = new FilteredElementCollector(doc, view.Id)
            .OfCategory(bic)
            .WhereElementIsNotElementType()
            .Where(e => !alreadyTagged.Contains(e.Id.Value))
            .ToList();

        var tagged = new List<long>();
        var skipped = new List<object>();

        foreach (var element in elements)
        {
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
                skipped.Add(new { id = element.Id.Value, reason = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new
        {
            view = view.Name,
            category,
            visible_in_view = elements.Count,
            already_tagged = alreadyTagged.Count,
            tagged_count = tagged.Count,
            skipped_count = skipped.Count
        });
    }

    private static XYZ GetTagPosition(Element element)
    {
        if (element.Location is LocationPoint lp) return lp.Point;
        if (element.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
        var bbox = element.get_BoundingBox(null);
        if (bbox != null) return (bbox.Min + bbox.Max) / 2;
        return XYZ.Zero;
    }
}
