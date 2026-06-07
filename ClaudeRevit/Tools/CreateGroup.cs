using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateGroup : IRevitTool
{
    public string Name => "create_group";

    public string Description =>
        "Creates a model group from the listed elements. Returns the group id and its group type id " +
        "(use the type id with place_group to drop more copies elsewhere). Optionally name the group.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Elements to group together.",
                items = new { type = "integer" }
            }),
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional group type name." })
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

        var group = doc.Create.NewGroup(ids);

        if (input.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            try { group.GroupType.Name = n.GetString(); }
            catch { /* name conflict */ }
        }

        return JsonSerializer.Serialize(new
        {
            id = group.Id.Value,
            group_type_id = group.GroupType.Id.Value,
            group_type_name = group.GroupType.Name,
            member_count = ids.Count
        });
    }
}
