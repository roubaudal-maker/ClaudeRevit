using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class DuplicateFamilyType : IRevitTool
{
    public string Name => "duplicate_family_type";

    public string Description =>
        "Creates a new family type by duplicating an existing FamilySymbol (e.g., a door type) and giving " +
        "it a new name. Optionally sets parameter values on the new type — keys are parameter names, values " +
        "are numbers (feet for lengths), strings, or booleans. Useful for 'I need a 36-inch wide door': " +
        "find a similar door type via list_family_types, duplicate it with new name, set Width = 3.0.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["source_type_id"] = JsonSerializer.SerializeToElement(new
            {
                type = "integer",
                description = "Element id of the FamilySymbol to duplicate."
            }),
            ["new_name"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Name for the new type (must be unique within the family)."
            }),
            ["parameters"] = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                description = "Optional. Parameter values to set on the new type. Keys are parameter names; " +
                              "values are numbers (feet for lengths), strings, or booleans. Read-only parameters are skipped."
            })
        },
        Required = ["source_type_id", "new_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var sourceId = new ElementId(input["source_type_id"].GetInt64());
        var newName = input["new_name"].GetString()!;

        var source = doc.GetElement(sourceId) as FamilySymbol
            ?? throw new InvalidOperationException(
                $"Element {sourceId.Value} is not a FamilySymbol. Use list_family_types to find one.");

        var newSymbol = source.Duplicate(newName) as FamilySymbol
            ?? throw new InvalidOperationException("Duplicate returned null.");

        var setParams = new List<object>();
        var skippedParams = new List<object>();

        if (input.TryGetValue("parameters", out var paramsObj) && paramsObj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in paramsObj.EnumerateObject())
            {
                var param = newSymbol.LookupParameter(prop.Name);
                if (param == null)
                {
                    skippedParams.Add(new { name = prop.Name, reason = "not found" });
                    continue;
                }
                if (param.IsReadOnly)
                {
                    skippedParams.Add(new { name = prop.Name, reason = "read-only" });
                    continue;
                }
                try
                {
                    SetParameter(param, prop.Value);
                    setParams.Add(new { name = prop.Name, value = prop.Value.ToString() });
                }
                catch (Exception ex)
                {
                    skippedParams.Add(new { name = prop.Name, reason = ex.Message });
                }
            }
        }

        return JsonSerializer.Serialize(new
        {
            new_type_id = newSymbol.Id.Value,
            family = newSymbol.FamilyName,
            new_name = newSymbol.Name,
            source_name = source.Name,
            set_count = setParams.Count,
            skipped_count = skippedParams.Count,
            set = setParams,
            skipped = skippedParams
        });
    }

    private static void SetParameter(Parameter param, JsonElement value)
    {
        switch (param.StorageType)
        {
            case StorageType.Double:
                param.Set(value.GetDouble());
                break;
            case StorageType.Integer:
                if (value.ValueKind == JsonValueKind.True) param.Set(1);
                else if (value.ValueKind == JsonValueKind.False) param.Set(0);
                else param.Set(value.GetInt32());
                break;
            case StorageType.String:
                param.Set(value.GetString() ?? "");
                break;
            case StorageType.ElementId:
                param.Set(new ElementId(value.GetInt64()));
                break;
            default:
                throw new InvalidOperationException($"Unsupported storage type: {param.StorageType}");
        }
    }
}
