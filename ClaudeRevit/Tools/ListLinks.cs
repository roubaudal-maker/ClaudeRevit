using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ListLinks : IRevitTool
{
    public string Name => "list_links";

    public string Description =>
        "Lists all linked files in the document: Revit links and CAD links (DWG / DXF / etc.). " +
        "Returns each link's id, file name, type, and load status.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>(),
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var revitLinks = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
            .Select(li =>
            {
                var lt = doc.GetElement(li.GetTypeId()) as RevitLinkType;
                return new
                {
                    instance_id = li.Id.Value,
                    type_id = li.GetTypeId().Value,
                    name = li.Name,
                    file = lt?.Name,
                    kind = "Revit",
                    loaded = lt != null ? RevitLinkType.IsLoaded(doc, lt.Id) : false
                };
            }).ToList();

        var cadInstances = new FilteredElementCollector(doc)
            .OfClass(typeof(ImportInstance)).Cast<ImportInstance>()
            .Select(ii =>
            {
                var cadType = doc.GetElement(ii.GetTypeId()) as CADLinkType;
                return new
                {
                    instance_id = ii.Id.Value,
                    type_id = ii.GetTypeId().Value,
                    name = ii.Name,
                    file = cadType?.Name ?? ii.Category?.Name,
                    kind = cadType != null ? "CAD Link" : "CAD Import",
                    loaded = (object?)null
                };
            }).ToList();

        return JsonSerializer.Serialize(new
        {
            revit_link_count = revitLinks.Count,
            revit_links = revitLinks,
            cad_count = cadInstances.Count,
            cad_links = cadInstances
        });
    }
}
