using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class LoadFamily : IRevitTool
{
    public string Name => "load_family";

    public string Description =>
        "Loads a Revit family file (.rfa) from a disk path into the active document. " +
        "If a family with the same name already exists, the new one replaces it (existing instance " +
        "parameter values are preserved). The user must supply an absolute file path.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["file_path"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Absolute path to the .rfa file (e.g., 'C:\\\\Path\\\\To\\\\Door.rfa')."
            })
        },
        Required = ["file_path"]
    };

    // LoadFamily manages its own transaction; don't wrap.
    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No document is open.");

        var path = input["file_path"].GetString()!;
        if (!File.Exists(path))
            throw new InvalidOperationException($"File not found: {path}");
        if (!path.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"File must be a .rfa family file (got: {Path.GetExtension(path)}).");

        if (!doc.LoadFamily(path, new SilentLoadOptions(), out var family))
            throw new InvalidOperationException(
                $"Revit rejected loading '{Path.GetFileName(path)}'. The family may already exist " +
                "with conflicting structure, or the file may be corrupt.");

        var symbolIds = family.GetFamilySymbolIds();
        return JsonSerializer.Serialize(new
        {
            family_id = family.Id.Value,
            family_name = family.Name,
            category = family.FamilyCategory?.Name,
            type_count = symbolIds.Count,
            type_ids = new List<long>(symbolIds.Count).Tap(list =>
            {
                foreach (var id in symbolIds) list.Add(id.Value);
            })
        });
    }

    private sealed class SilentLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = false;
            return true;
        }

        public bool OnSharedFamilyFound(
            Family sharedFamily, bool familyInUse,
            out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Project;
            overwriteParameterValues = false;
            return true;
        }
    }
}

internal static class ListTapExtensions
{
    public static List<T> Tap<T>(this List<T> list, Action<List<T>> action)
    {
        action(list);
        return list;
    }
}
