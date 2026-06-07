using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class PinElements : IRevitTool
{
    public string Name => "pin_elements";

    public string Description =>
        "Pins elements so they can't be accidentally moved. Equivalent to Revit's Pin command.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Elements to pin.",
                items = new { type = "integer" }
            })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        int pinned = 0;
        var skipped = new List<object>();
        foreach (var id in ids)
        {
            var el = doc.GetElement(id);
            if (el == null) { skipped.Add(new { id = id.Value, reason = "not found" }); continue; }
            try { el.Pinned = true; pinned++; }
            catch (Exception ex) { skipped.Add(new { id = id.Value, reason = ex.Message }); }
        }

        return JsonSerializer.Serialize(new { pinned, skipped_count = skipped.Count, skipped });
    }
}
