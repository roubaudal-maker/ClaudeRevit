using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateSketchPlane : IRevitTool
{
    public string Name => "create_sketch_plane";

    public string Description =>
        "Creates a sketch plane element from a normal vector and an origin point. Returns the sketch-plane id, " +
        "useful as input to view-based sketching workflows.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["origin_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Origin X (feet)." }),
            ["origin_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Origin Y (feet)." }),
            ["origin_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Origin Z (feet)." }),
            ["normal_x"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Normal vector X." }),
            ["normal_y"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Normal vector Y." }),
            ["normal_z"] = JsonSerializer.SerializeToElement(new { type = "number", description = "Normal vector Z." })
        },
        Required = ["origin_x", "origin_y", "origin_z", "normal_x", "normal_y", "normal_z"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var origin = new XYZ(input["origin_x"].GetDouble(), input["origin_y"].GetDouble(), input["origin_z"].GetDouble());
        var normal = new XYZ(input["normal_x"].GetDouble(), input["normal_y"].GetDouble(), input["normal_z"].GetDouble());
        if (normal.IsZeroLength())
            throw new InvalidOperationException("Normal vector cannot be zero.");
        normal = normal.Normalize();

        var plane = Plane.CreateByNormalAndOrigin(normal, origin);
        var sp = SketchPlane.Create(doc, plane);

        return JsonSerializer.Serialize(new
        {
            id = sp.Id.Value,
            type = "SketchPlane",
            origin_ft = new { x = origin.X, y = origin.Y, z = origin.Z },
            normal = new { x = normal.X, y = normal.Y, z = normal.Z }
        });
    }
}
