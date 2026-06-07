using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateStructuralColumn : IRevitTool
{
    public string Name => "create_structural_column";

    public string Description =>
        "Places a structural column at a plan-coordinate point (in feet) on a named level. " +
        "If type_name is omitted, the first available structural column type is used.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Column X (feet)." }),
            ["y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Column Y (feet)." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Base level name." }),
            ["type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional column type name." })
        },
        Required = ["x", "y", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var x = input["x"].GetDouble();
        var y = input["y"].GetDouble();
        var levelName = input["level_name"].GetString()!;

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        FamilySymbol symbol;
        if (input.TryGetValue("type_name", out var tn) && tn.ValueKind == JsonValueKind.String)
        {
            var name = tn.GetString();
            symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name == name)
                ?? throw new InvalidOperationException($"Structural column type '{name}' not found.");
        }
        else
        {
            symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No structural column types loaded.");
        }

        if (!symbol.IsActive) symbol.Activate();
        doc.Regenerate();

        var instance = doc.Create.NewFamilyInstance(
            new XYZ(x, y, level.Elevation), symbol, level, StructuralType.Column);

        return JsonSerializer.Serialize(new
        {
            id = instance.Id.Value,
            type = "StructuralColumn",
            family = symbol.FamilyName,
            type_name = symbol.Name,
            level = level.Name,
            position_ft = new { x, y }
        });
    }
}
