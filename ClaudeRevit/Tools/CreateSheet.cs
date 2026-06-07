using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class CreateSheet : IRevitTool
{
    public string Name => "create_sheet";

    public string Description =>
        "Creates a new title sheet in the document. Optionally specify a title-block family symbol name and " +
        "set the sheet's number and name. If no title block is specified, the first available is used " +
        "(or a blank sheet if none are loaded).";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["sheet_number"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Sheet number (e.g., 'A101'). Must be unique." }),
            ["sheet_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Sheet name (e.g., 'Floor Plans')." }),
            ["title_block_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Optional title-block family symbol name." })
        },
        Required = ["sheet_number", "sheet_name"]
    };

    public bool RequiresTransaction => true;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var number = input["sheet_number"].GetString()!;
        var name = input["sheet_name"].GetString()!;

        var titleBlockId = ElementId.InvalidElementId;
        if (input.TryGetValue("title_block_name", out var tbn) && tbn.ValueKind == JsonValueKind.String)
        {
            var tbName = tbn.GetString();
            var symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name == tbName)
                ?? throw new InvalidOperationException($"Title block '{tbName}' not found.");
            titleBlockId = symbol.Id;
        }
        else
        {
            var firstTb = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault();
            if (firstTb != null) titleBlockId = firstTb.Id;
        }

        var sheet = ViewSheet.Create(doc, titleBlockId);
        sheet.SheetNumber = number;
        sheet.Name = name;

        return JsonSerializer.Serialize(new
        {
            id = sheet.Id.Value,
            type = "Sheet",
            number = sheet.SheetNumber,
            name = sheet.Name
        });
    }
}
