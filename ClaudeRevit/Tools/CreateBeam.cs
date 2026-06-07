using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateBeam : IRevitTool
{
    public string Name => "create_beam";

    public string Description =>
        "Creates a straight structural beam between two plan-coordinate points (in feet) on a named reference level. " +
        "If type_name is omitted, the first available structural framing type is used.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Beam start X (feet)." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Beam start Y (feet)." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Beam end X (feet)." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Beam end Y (feet)." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Reference level name." }),
            ["type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional beam type name." })
        },
        Required = ["start_x", "start_y", "end_x", "end_y", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var sx = input["start_x"].GetDouble();
        var sy = input["start_y"].GetDouble();
        var ex = input["end_x"].GetDouble();
        var ey = input["end_y"].GetDouble();
        var levelName = input["level_name"].GetString()!;

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        var start = new XYZ(sx, sy, level.Elevation);
        var end = new XYZ(ex, ey, level.Elevation);
        if (start.IsAlmostEqualTo(end))
            throw new InvalidOperationException("Beam has zero length.");

        FamilySymbol symbol;
        if (input.TryGetValue("type_name", out var tn) && tn.ValueKind == JsonValueKind.String)
        {
            var name = tn.GetString();
            symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name == name)
                ?? throw new InvalidOperationException($"Beam type '{name}' not found.");
        }
        else
        {
            symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No structural framing types loaded.");
        }

        if (!symbol.IsActive) symbol.Activate();
        doc.Regenerate();

        var line = Line.CreateBound(start, end);
        var instance = doc.Create.NewFamilyInstance(line, symbol, level, StructuralType.Beam);

        return JsonSerializer.Serialize(new
        {
            id = instance.Id.Value,
            type = "Beam",
            family = symbol.FamilyName,
            type_name = symbol.Name,
            level = level.Name,
            length_ft = line.Length
        });
    }
}
