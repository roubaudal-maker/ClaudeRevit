using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateMaterial : IRevitTool
{
    public string Name => "create_material";

    public string Description =>
        "Creates a new material with a name and optional color (RGB 0-255). " +
        "Use set_element_material to assign this material to elements afterward.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Material name (must be unique)." }),
            ["r"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, maximum = 255, description = "Red." }),
            ["g"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, maximum = 255, description = "Green." }),
            ["b"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, maximum = 255, description = "Blue." }),
            ["transparency"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, maximum = 100, description = "Transparency 0-100 (default 0)." })
        },
        Required = ["name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var name = input["name"].GetString()!;
        var matId = Material.Create(doc, name);
        var material = doc.GetElement(matId) as Material
            ?? throw new InvalidOperationException("Material.Create returned null.");

        var hasR = input.TryGetValue("r", out var rEl);
        var hasG = input.TryGetValue("g", out var gEl);
        var hasB = input.TryGetValue("b", out var bEl);
        if (hasR && hasG && hasB)
        {
            material.Color = new Color((byte)rEl.GetInt32(), (byte)gEl.GetInt32(), (byte)bEl.GetInt32());
        }

        if (input.TryGetValue("transparency", out var t) && t.ValueKind == JsonValueKind.Number)
        {
            material.Transparency = t.GetInt32();
        }

        return JsonSerializer.Serialize(new
        {
            id = matId.Value,
            name = material.Name,
            color = (hasR && hasG && hasB) ? new { r = rEl.GetInt32(), g = gEl.GetInt32(), b = bEl.GetInt32() } : null,
            transparency = material.Transparency
        });
    }
}
