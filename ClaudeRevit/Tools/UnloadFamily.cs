using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class UnloadFamily : IRevitTool
{
    public string Name => "unload_family";

    public string Description =>
        "Removes a loaded family from the document. All instances of this family must already be deleted, " +
        "or the deletion will fail. Use list_loaded_families to find the family_id.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["family_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Family id (from list_loaded_families)." })
        },
        Required = ["family_id"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var id = new ElementId(input["family_id"].GetInt64());
        var family = doc.GetElement(id) as Family
            ?? throw new InvalidOperationException($"Element {id.Value} is not a Family.");

        var familyName = family.Name;
        var deleted = doc.Delete(id);

        return JsonSerializer.Serialize(new
        {
            removed_family = familyName,
            deleted_count = deleted.Count
        });
    }
}
