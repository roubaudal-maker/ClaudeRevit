using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace ClaudeRevit.Services;

public static class SelectionService
{
    public sealed class SelectionInfo
    {
        public IReadOnlyList<long> Ids { get; init; } = Array.Empty<long>();
        public IReadOnlyDictionary<string, int> CategoryCounts { get; init; } =
            new Dictionary<string, int>();
        public string Description { get; init; } = "";
    }

    private static readonly SelectionInfo Empty = new();
    public static SelectionInfo Current { get; private set; } = Empty;
    public static event Action<SelectionInfo>? Changed;

    private static HashSet<long> _lastIds = new();

    public static void Initialize(UIControlledApplication app)
    {
        app.Idling += OnIdling;
    }

    private static void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (sender is not UIApplication uiApp) return;
        var uidoc = uiApp.ActiveUIDocument;
        if (uidoc == null)
        {
            if (_lastIds.Count > 0) PublishEmpty();
            return;
        }

        var ids = uidoc.Selection.GetElementIds();
        var newIds = new HashSet<long>(ids.Select(i => i.Value));
        if (newIds.SetEquals(_lastIds)) return;

        _lastIds = newIds;

        if (newIds.Count == 0)
        {
            PublishEmpty();
            return;
        }

        var doc = uidoc.Document;
        var byCategory = new Dictionary<string, int>();
        foreach (var id in ids)
        {
            var el = doc.GetElement(id);
            var cat = el?.Category?.Name ?? "(no category)";
            byCategory[cat] = byCategory.GetValueOrDefault(cat) + 1;
        }

        var description = byCategory.Count == 1
            ? $"{newIds.Count} {byCategory.Keys.First()}"
            : $"{newIds.Count} elements ({string.Join(", ", byCategory.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Value} {kv.Key}"))})";

        Current = new SelectionInfo
        {
            Ids = newIds.ToList(),
            CategoryCounts = byCategory,
            Description = description
        };
        Changed?.Invoke(Current);
    }

    private static void PublishEmpty()
    {
        if (Current == Empty) return;
        Current = Empty;
        Changed?.Invoke(Current);
    }
}
