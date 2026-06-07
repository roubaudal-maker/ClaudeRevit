using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateRevisionCloud : IRevitTool
{
    public string Name => "create_revision_cloud";

    public string Description =>
        "Creates a revision cloud in a view from a closed boundary of plan-coordinate points (in feet). " +
        "If revision_id is not given, uses the latest existing revision or creates a new one. " +
        "Min 3 points; closes automatically.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional view id (default active view)." }),
            ["points"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 3,
                description = "Boundary points {x, y} in feet, in order.",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" }
                    },
                    required = new[] { "x", "y" }
                }
            }),
            ["revision_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Optional Revision element id." })
        },
        Required = ["points"]
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

        var pts = input["points"].EnumerateArray()
            .Select(p => new XYZ(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble(), 0))
            .ToList();
        if (pts.Count < 3)
            throw new InvalidOperationException($"Revision cloud needs at least 3 points (got {pts.Count}).");

        ElementId revisionId;
        if (input.TryGetValue("revision_id", out var rid) && rid.ValueKind == JsonValueKind.Number)
        {
            revisionId = new ElementId(rid.GetInt64());
        }
        else
        {
            var existing = Revision.GetAllRevisionIds(doc);
            if (existing.Count > 0)
            {
                revisionId = existing.Last();
            }
            else
            {
                var rev = Revision.Create(doc);
                revisionId = rev.Id;
            }
        }

        var curves = new List<Curve>();
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            if (a.IsAlmostEqualTo(b))
                throw new InvalidOperationException($"Boundary points {i} and {(i + 1) % pts.Count} are identical.");
            curves.Add(Line.CreateBound(a, b));
        }

        var cloud = RevisionCloud.Create(doc, view, revisionId, curves);

        var revision = doc.GetElement(revisionId) as Revision;
        return JsonSerializer.Serialize(new
        {
            id = cloud.Id.Value,
            type = "RevisionCloud",
            view = view.Name,
            revision_id = revisionId.Value,
            revision_description = revision?.Description,
            boundary_points = pts.Count
        });
    }
}
