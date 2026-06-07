using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetProjectInfo : IRevitTool
{
    public string Name => "get_project_info";

    public string Description =>
        "Returns the document's Project Information: name, number, organization, client, address, status, issue date.";

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

        var pi = doc.ProjectInformation;
        return JsonSerializer.Serialize(new
        {
            name = pi?.Name,
            number = pi?.Number,
            organization_name = pi?.OrganizationName,
            organization_description = pi?.OrganizationDescription,
            client_name = pi?.ClientName,
            address = pi?.Address,
            status = pi?.Status,
            issue_date = pi?.IssueDate,
            building_name = pi?.BuildingName,
            author = pi?.Author
        });
    }
}
