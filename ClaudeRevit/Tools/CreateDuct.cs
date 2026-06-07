using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateDuct : IRevitTool
{
    public string Name => "create_duct";

    public string Description =>
        "Creates a straight duct between two points (in feet) on a level. If duct_type_name or " +
        "system_type_name are omitted, the first available is used. Needs duct families loaded; " +
        "this is a mechanical (MEP) tool — won't work in pure-architectural templates.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["start_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start X (feet)." }),
            ["start_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start Y (feet)." }),
            ["start_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Start Z (feet)." }),
            ["end_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End X (feet)." }),
            ["end_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End Y (feet)." }),
            ["end_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "End Z (feet)." }),
            ["level_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Reference level name." }),
            ["duct_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional duct type name." }),
            ["system_type_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional duct system type name (e.g. 'Supply Air')." })
        },
        Required = ["start_x", "start_y", "start_z", "end_x", "end_y", "end_z", "level_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var levelName = input["level_name"].GetString()!;
        var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName)
            ?? throw new InvalidOperationException($"Level '{levelName}' not found.");

        var start = new XYZ(input["start_x"].GetDouble(), input["start_y"].GetDouble(), input["start_z"].GetDouble());
        var end = new XYZ(input["end_x"].GetDouble(), input["end_y"].GetDouble(), input["end_z"].GetDouble());
        if (start.IsAlmostEqualTo(end))
            throw new InvalidOperationException("Duct has zero length.");

        DuctType ductType;
        if (input.TryGetValue("duct_type_name", out var dtn) && dtn.ValueKind == JsonValueKind.String)
        {
            var name = dtn.GetString();
            ductType = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>()
                .FirstOrDefault(t => t.Name == name)
                ?? throw new InvalidOperationException($"Duct type '{name}' not found.");
        }
        else
        {
            ductType = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No duct types loaded. This document might not be a mechanical template — load duct families first.");
        }

        MechanicalSystemType systemType;
        if (input.TryGetValue("system_type_name", out var stn) && stn.ValueKind == JsonValueKind.String)
        {
            var name = stn.GetString();
            systemType = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>()
                .FirstOrDefault(t => t.Name == name)
                ?? throw new InvalidOperationException($"Mechanical system type '{name}' not found.");
        }
        else
        {
            systemType = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No mechanical system types in this document.");
        }

        var duct = Duct.Create(doc, systemType.Id, ductType.Id, level.Id, start, end);

        return JsonSerializer.Serialize(new
        {
            id = duct.Id.Value,
            type = "Duct",
            duct_type = ductType.Name,
            system_type = systemType.Name,
            level = level.Name,
            length_ft = (end - start).GetLength()
        });
    }
}
