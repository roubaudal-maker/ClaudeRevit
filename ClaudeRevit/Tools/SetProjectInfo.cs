using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class SetProjectInfo : IRevitTool
{
    public string Name => "set_project_info";

    public string Description =>
        "Updates fields on the document's Project Information element. Pass only the fields you want to set; " +
        "omitted fields are left untouched.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Project name." }),
            ["number"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Project number." }),
            ["organization_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Owner / organization name." }),
            ["organization_description"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Description of the owner organization." }),
            ["client_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Client name." }),
            ["address"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Project address." }),
            ["status"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Project status (e.g. 'Design Development', 'Construction')." }),
            ["building_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Building name." }),
            ["author"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Author." }),
            ["issue_date"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Issue date (free-form string)." })
        },
        Required = []
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var pi = doc.ProjectInformation
            ?? throw new InvalidOperationException("Document has no ProjectInformation element.");

        var updated = new List<string>();

        void TrySet(string key, Action<string> setter)
        {
            if (input.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            {
                try { setter(el.GetString()!); updated.Add(key); } catch { }
            }
        }

        TrySet("name", v => pi.Name = v);
        TrySet("number", v => pi.Number = v);
        TrySet("organization_name", v => pi.OrganizationName = v);
        TrySet("organization_description", v => pi.OrganizationDescription = v);
        TrySet("client_name", v => pi.ClientName = v);
        TrySet("address", v => pi.Address = v);
        TrySet("status", v => pi.Status = v);
        TrySet("building_name", v => pi.BuildingName = v);
        TrySet("author", v => pi.Author = v);
        TrySet("issue_date", v => pi.IssueDate = v);

        return JsonSerializer.Serialize(new
        {
            updated_fields = updated,
            current = new
            {
                name = pi.Name,
                number = pi.Number,
                organization_name = pi.OrganizationName,
                client_name = pi.ClientName,
                status = pi.Status
            }
        });
    }
}
