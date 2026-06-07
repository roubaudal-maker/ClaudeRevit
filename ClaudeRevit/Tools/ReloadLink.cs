using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ReloadLink : IRevitTool
{
    public string Name => "reload_link";

    public string Description =>
        "Reloads a Revit or CAD link to pick up changes from the source file. " +
        "Pass either the link instance id or the link type id (use list_links to get both).";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["link_id"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Either the instance id or the type id of the link." })
        },
        Required = ["link_id"]
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var id = new ElementId(input["link_id"].GetInt64());
        var el = doc.GetElement(id)
            ?? throw new InvalidOperationException($"Element {id.Value} not found.");

        ElementId typeId;
        if (el is RevitLinkInstance rli) typeId = rli.GetTypeId();
        else if (el is ImportInstance ii) typeId = ii.GetTypeId();
        else if (el is RevitLinkType || el is CADLinkType) typeId = id;
        else throw new InvalidOperationException("Element is not a link instance or link type.");

        var typeEl = doc.GetElement(typeId);
        switch (typeEl)
        {
            case RevitLinkType rlt:
                var result = rlt.Reload();
                return JsonSerializer.Serialize(new
                {
                    kind = "Revit",
                    name = rlt.Name,
                    status = result.LoadResult.ToString()
                });
            case CADLinkType cad:
                cad.Reload();
                return JsonSerializer.Serialize(new
                {
                    kind = "CAD",
                    name = cad.Name,
                    status = "reloaded"
                });
            default:
                throw new InvalidOperationException(
                    $"Cannot reload — element {id.Value} is not a Revit or CAD link.");
        }
    }
}
