using System;
using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateRevision : IRevitTool
{
    public string Name => "create_revision";

    public string Description =>
        "Adds a new revision entry to the document's revision sequence. Optionally set description, " +
        "issued-by, and issued-to fields. Returns the revision id (use with create_revision_cloud).";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["description"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Revision description." }),
            ["issued_by"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Issued-by field." }),
            ["issued_to"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Issued-to field." })
        },
        Required = []
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var rev = Revision.Create(doc);

        if (input.TryGetValue("description", out var d) && d.ValueKind == JsonValueKind.String)
            rev.Description = d.GetString();
        if (input.TryGetValue("issued_by", out var ib) && ib.ValueKind == JsonValueKind.String)
            rev.IssuedBy = ib.GetString();
        if (input.TryGetValue("issued_to", out var it) && it.ValueKind == JsonValueKind.String)
            rev.IssuedTo = it.GetString();

        return JsonSerializer.Serialize(new
        {
            id = rev.Id.Value,
            description = rev.Description,
            issued_by = rev.IssuedBy,
            issued_to = rev.IssuedTo,
            sequence_number = rev.SequenceNumber
        });
    }
}
