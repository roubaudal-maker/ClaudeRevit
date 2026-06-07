using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class AnalyzeWarnings : IRevitTool
{
    public string Name => "analyze_warnings";

    public string Description =>
        "Returns the list of warnings currently in the model: descriptions, severity, and the affected element ids. " +
        "Useful when the user asks 'what's wrong with my model?' or to triage an audit. Read-only.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["limit"] = JsonSerializer.SerializeToElement(new
            {
                type = "integer",
                description = "Maximum number of warnings to return (default 50, max 500).",
                minimum = 1,
                maximum = 500
            })
        },
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var limit = input.TryGetValue("limit", out var l) ? l.GetInt32() : 50;
        if (limit < 1 || limit > 500) limit = 50;

        var warnings = doc.GetWarnings();
        var truncated = warnings.Count > limit;

        var entries = warnings.Take(limit).Select(w => new
        {
            description = w.GetDescriptionText(),
            severity = w.GetSeverity().ToString(),
            failing_ids = w.GetFailingElements().Select(id => id.Value).ToList(),
            affected_ids = w.GetAdditionalElements().Select(id => id.Value).ToList()
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            total_warnings = warnings.Count,
            returned = entries.Count,
            truncated,
            warnings = entries
        });
    }
}
