using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateView : IRevitTool
{
    public string Name => "create_view";

    public string Description =>
        "Creates a new view. Currently supports floor plans and ceiling plans for a given level. " +
        "Use 'floor_plan' or 'ceiling_plan' for view_type.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["view_type"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Type of view to create.",
                @enum = new[] { "floor_plan", "ceiling_plan" }
            }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Level the plan belongs to." }),
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional view name." })
        },
        Required = ["view_type", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var viewType = input["view_type"].GetString()!.ToLowerInvariant();
        var levelName = input["level_name"].GetString()!;

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        var family = viewType switch
        {
            "floor_plan" => ViewFamily.FloorPlan,
            "ceiling_plan" => ViewFamily.CeilingPlan,
            _ => throw new InvalidOperationException(
                $"Unsupported view_type '{viewType}'. Use 'floor_plan' or 'ceiling_plan'.")
        };

        var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.ViewFamily == family)
            ?? throw new InvalidOperationException($"No ViewFamilyType for {family} found in this document.");

        var view = ViewPlan.Create(doc, vft.Id, level.Id);

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { view.Name = n.GetString(); }
            catch (Exception ex) { throw new InvalidOperationException($"Could not set view name: {ex.Message}"); }
        }

        return JsonSerializer.Serialize(new
        {
            id = view.Id.Value,
            type = "View",
            view_family = family.ToString(),
            name = view.Name,
            level = level.Name
        });
    }
}
