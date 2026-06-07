using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class PlaceFamilyInstance : IRevitTool
{
    public string Name => "place_family_instance";

    public string Description =>
        "Generic placement of a family instance at a plan-coordinate point (in feet) on a named level. " +
        "Use this for furniture, equipment, generic models, plumbing fixtures, lighting, etc. " +
        "First call list_family_types to find the right family_type_id. For walls-doors-windows or " +
        "structural beams/columns, use the more-specific dedicated tools.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["family_type_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "FamilySymbol id (from list_family_types)." }),
            ["x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Placement X (feet)." }),
            ["y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Placement Y (feet)." }),
            ["z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Placement Z (feet, optional)." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Host level name (if level-hosted)." }),
            ["rotation_deg"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Optional rotation around Z axis in degrees (CCW positive)." })
        },
        Required = ["family_type_id", "x", "y"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var typeId = new ElementId(input["family_type_id"].GetInt64());
        var symbol = doc.GetElement(typeId) as FamilySymbol
            ?? throw new InvalidOperationException($"Element {typeId.Value} is not a FamilySymbol.");

        if (!symbol.IsActive) symbol.Activate();
        doc.Regenerate();

        var x = input["x"].GetDouble();
        var y = input["y"].GetDouble();
        var z = input.TryGetValue("z", out var zEl) ? zEl.GetDouble() : 0.0;
        var point = new XYZ(x, y, z);

        Level? level = null;
        if (input.TryGetValue("level_name", out var ln) && ln.ValueKind == JsonValueKind.String)
        {
            var name = ln.GetString();
            level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .FirstOrDefault(l => l.Name == name)
                ?? throw new InvalidOperationException($"Level '{name}' not found.");
        }

        FamilyInstance instance = level != null
            ? doc.Create.NewFamilyInstance(point, symbol, level, StructuralType.NonStructural)
            : doc.Create.NewFamilyInstance(point, symbol, StructuralType.NonStructural);

        if (input.TryGetValue("rotation_deg", out var rd) && rd.ValueKind == JsonValueKind.Number)
        {
            var rad = rd.GetDouble() * Math.PI / 180.0;
            var axis = Line.CreateBound(point, point + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(doc, instance.Id, axis, rad);
        }

        return JsonSerializer.Serialize(new
        {
            id = instance.Id.Value,
            type = "FamilyInstance",
            family = symbol.FamilyName,
            type_name = symbol.Name,
            level = level?.Name,
            position_ft = new { x, y, z }
        });
    }
}
