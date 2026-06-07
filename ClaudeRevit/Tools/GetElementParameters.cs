using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetElementParameters : IRevitTool
{
    public string Name => "get_element_parameters";

    public string Description =>
        "Returns all parameters of one or more elements: name, value (formatted), storage type, and read-only flag. " +
        "Use this when the user asks 'what's the height of this wall?' / 'what mark is door 5?' or to inspect " +
        "any element in detail. If 'parameter_names' is supplied, only those parameters are returned.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                minItems = 1,
                description = "Element ids to inspect.",
                items = new { type = "integer" }
            }),
            ["parameter_names"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                description = "Optional whitelist of parameter names. Omit to return all parameters.",
                items = new { type = "string" }
            })
        },
        Required = ["element_ids"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var ids = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        HashSet<string>? whitelist = null;
        if (input.TryGetValue("parameter_names", out var pn) && pn.ValueKind == JsonValueKind.Array)
            whitelist = pn.EnumerateArray().Select(e => e.GetString() ?? "").ToHashSet();

        var results = ids.Select(id =>
        {
            var el = doc.GetElement(id);
            if (el == null) return new { id = id.Value, error = "not found", category = (string?)null, name = (string?)null, parameters = (object?)null };

            var parameters = el.Parameters.Cast<Parameter>()
                .Where(p => whitelist == null || whitelist.Contains(p.Definition?.Name ?? ""))
                .Select(p => new
                {
                    name = p.Definition?.Name ?? "",
                    value = FormatParameter(p),
                    storage = p.StorageType.ToString(),
                    read_only = p.IsReadOnly
                })
                .ToList();

            return new
            {
                id = id.Value,
                error = (string?)null,
                category = el.Category?.Name,
                name = (string?)el.Name,
                parameters = (object?)parameters
            };
        }).ToList();

        return JsonSerializer.Serialize(results);
    }

    private static string FormatParameter(Parameter p) => p.StorageType switch
    {
        StorageType.String => p.AsString() ?? "",
        StorageType.Integer => p.AsInteger().ToString(),
        StorageType.Double => p.AsValueString() ?? p.AsDouble().ToString("F4"),
        StorageType.ElementId => p.AsElementId() == ElementId.InvalidElementId ? "(none)" : p.AsElementId().Value.ToString(),
        _ => p.AsValueString() ?? "(none)"
    };
}
