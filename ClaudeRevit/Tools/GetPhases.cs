using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetPhases : IRevitTool
{
    public string Name => "get_phases";

    public string Description =>
        "Returns the project phases in chronological order, with id and name.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>(),
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var phases = doc.Phases.Cast<Phase>()
            .Select(p => new { id = p.Id.Value, name = p.Name })
            .ToList();

        return JsonSerializer.Serialize(new { count = phases.Count, phases });
    }
}
