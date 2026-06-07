using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetElementMaterial : IRevitTool
{
    public string Name => "set_element_material";

    public string Description =>
        "Assigns a material to one or more elements by setting a material-typed parameter on them. " +
        "If 'parameter_name' is omitted, tries common names like 'Material', 'Structural Material', " +
        "'Glass Material'. For walls and floors, layer-level material assignment requires editing the " +
        "type's compound structure (not yet supported by this tool).";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["element_ids"] = JsonSerializer.SerializeToElement(new
            {
                type = "array", minItems = 1,
                description = "Elements to assign the material to.",
                items = new { type = "integer" }
            }),
            ["material_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Material id (from list_materials or create_material)." }),
            ["parameter_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional explicit parameter name (e.g. 'Material', 'Structural Material')." })
        },
        Required = ["element_ids", "material_id"]
    };

    public bool RequiresTransaction => true;

    private static readonly string[] CommonMaterialParamNames =
    {
        "Material",
        "Structural Material",
        "Glass Material",
        "Frame Material",
        "Sash Material"
    };

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var materialId = new ElementId(input["material_id"].GetInt64());
        var material = doc.GetElement(materialId) as Material
            ?? throw new InvalidOperationException($"Element {materialId.Value} is not a Material.");

        var explicitParam = input.TryGetValue("parameter_name", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()!
            : null;

        var elementIds = input["element_ids"].EnumerateArray()
            .Select(e => new ElementId(e.GetInt64())).ToList();

        var updated = new List<long>();
        var skipped = new List<object>();

        foreach (var id in elementIds)
        {
            var el = doc.GetElement(id);
            if (el == null) { skipped.Add(new { id = id.Value, reason = "not found" }); continue; }

            Parameter? param = null;
            string? usedName = null;

            if (explicitParam != null)
            {
                param = el.LookupParameter(explicitParam);
                if (param == null) { skipped.Add(new { id = id.Value, reason = $"no parameter '{explicitParam}'" }); continue; }
                usedName = explicitParam;
            }
            else
            {
                foreach (var name in CommonMaterialParamNames)
                {
                    var candidate = el.LookupParameter(name);
                    if (candidate != null && candidate.StorageType == StorageType.ElementId && !candidate.IsReadOnly)
                    {
                        param = candidate;
                        usedName = name;
                        break;
                    }
                }
                if (param == null)
                {
                    skipped.Add(new { id = id.Value, reason = "no common material parameter found — specify parameter_name" });
                    continue;
                }
            }

            try { param.Set(materialId); updated.Add(id.Value); }
            catch (Exception ex) { skipped.Add(new { id = id.Value, reason = ex.Message, parameter = usedName }); }
        }

        return JsonSerializer.Serialize(new
        {
            material = material.Name,
            updated_count = updated.Count,
            skipped_count = skipped.Count,
            skipped
        });
    }
}
