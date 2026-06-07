using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ApplyViewTemplate : IRevitTool
{
    public string Name => "apply_view_template";

    public string Description =>
        "Applies a view template to one or more views. Specify the template by id or name.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Views to apply the template to.",
                items = new { type = "integer" }
            }),
            ["template_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "View template id." }),
            ["template_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "View template name (exact match)." })
        },
        Required = ["view_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        View? template = null;
        if (input.TryGetValue("template_id", out var tid) && tid.ValueKind == JsonValueKind.Number)
        {
            template = doc.GetElement(new ElementId(tid.GetInt64())) as View;
        }
        else if (input.TryGetValue("template_name", out var tn) && tn.ValueKind == JsonValueKind.String)
        {
            var name = tn.GetString();
            template = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .FirstOrDefault(v => v.IsTemplate && v.Name == name);
        }

        if (template == null || !template.IsTemplate)
            throw new InvalidOperationException("Template not found (or specified view is not a template).");

        var viewIds = input["view_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var applied = new List<long>();
        var skipped = new List<object>();
        foreach (var id in viewIds)
        {
            var v = doc.GetElement(id) as View;
            if (v == null) { skipped.Add(new { id = id.Value, reason = "not a view" }); continue; }
            try { v.ViewTemplateId = template.Id; applied.Add(id.Value); }
            catch (Exception ex) { skipped.Add(new { id = id.Value, reason = ex.Message }); }
        }

        return JsonSerializer.Serialize(new
        {
            template = template.Name,
            applied_count = applied.Count,
            applied_ids = applied,
            skipped
        });
    }
}
