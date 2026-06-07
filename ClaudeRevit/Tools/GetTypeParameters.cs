using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetTypeParameters : IRevitTool
{
    public string Name => "get_type_parameters";

    public string Description =>
        "Returns all parameters of an element TYPE (FamilySymbol, WallType, FloorType, etc.). " +
        "Useful before calling set_type_parameter to discover names + storage types.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["type_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "ElementType id." })
        },
        Required = ["type_id"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var typeId = new ElementId(input["type_id"].GetInt64());
        var type = doc.GetElement(typeId) as ElementType
            ?? throw new InvalidOperationException($"Element {typeId.Value} is not an ElementType.");

        var parameters = type.Parameters.Cast<Parameter>().Select(p => new
        {
            name = p.Definition?.Name ?? "",
            value = FormatParameter(p),
            storage = p.StorageType.ToString(),
            read_only = p.IsReadOnly
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            id = typeId.Value,
            family = type.FamilyName,
            type_name = type.Name,
            parameters
        });
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
