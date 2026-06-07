using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetParameter : IRevitTool
{
    public string Name => "set_parameter";

    public string Description =>
        "Sets a parameter value on a single element. The element is identified by id, the parameter by name. " +
        "Numeric parameters expect feet for length and square feet for area (Revit's internal units) — convert from meters first. " +
        "Use query_elements to discover an element's parameters by reading its returned data.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Target element id." }),
            ["parameter_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Parameter name (case-sensitive)." }),
            ["value"] = JsonSerializer.SerializeToElement(new
            {
                description = "New value. String for text params, number for length/integer/double params, boolean for yes/no params.",
                oneOf = new object[]
                {
                    new { type = "string" },
                    new { type = "number" },
                    new { type = "boolean" },
                    new { type = "integer" }
                }
            })
        },
        Required = ["element_id", "parameter_name", "value"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var id = new ElementId(input["element_id"].GetInt64());
        var element = doc.GetElement(id)
            ?? throw new InvalidOperationException($"Element {id.Value} not found.");

        var paramName = input["parameter_name"].GetString()!;
        var param = element.LookupParameter(paramName)
            ?? throw new InvalidOperationException(
                $"Parameter '{paramName}' not found on element {id.Value} ({element.Category?.Name}).");

        if (param.IsReadOnly)
            throw new InvalidOperationException($"Parameter '{paramName}' is read-only.");

        var value = input["value"];
        bool ok = param.StorageType switch
        {
            StorageType.String => param.Set(value.GetString() ?? ""),
            StorageType.Integer => param.Set(value.ValueKind == JsonValueKind.True ? 1
                                          : value.ValueKind == JsonValueKind.False ? 0
                                          : value.GetInt32()),
            StorageType.Double => param.Set(value.GetDouble()),
            StorageType.ElementId => param.Set(new ElementId(value.GetInt64())),
            _ => throw new InvalidOperationException($"Unsupported parameter storage type: {param.StorageType}")
        };

        if (!ok)
            throw new InvalidOperationException(
                $"Failed to set '{paramName}' — value may be invalid for this parameter's type.");

        return JsonSerializer.Serialize(new
        {
            element_id = id.Value,
            parameter = paramName,
            storage_type = param.StorageType.ToString(),
            new_value = param.AsValueString() ?? param.AsString() ?? value.ToString()
        });
    }
}
