using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class ToolDispatcher : IExternalEventHandler
{
    private static ToolDispatcher? _instance;

    public static ToolDispatcher Instance =>
        _instance ?? throw new InvalidOperationException("ToolDispatcher.Initialize must be called first.");

    public static void Initialize(ToolRegistry registry)
    {
        if (_instance != null) return;
        _instance = new ToolDispatcher(registry);
        _instance._event = ExternalEvent.Create(_instance);
    }

    private readonly ToolRegistry _registry;
    private ExternalEvent _event = null!;
    private readonly ConcurrentQueue<Job> _queue = new();
    private TransactionGroup? _activeGroup;

    private ToolDispatcher(ToolRegistry registry) => _registry = registry;

    public Task BeginTurnAsync(string label, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));
        _queue.Enqueue(new BeginTurnJob(label, tcs));
        _event.Raise();
        return tcs.Task;
    }

    public Task EndTurnAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));
        _queue.Enqueue(new EndTurnJob(tcs));
        _event.Raise();
        return tcs.Task;
    }

    public Task<string> ExecuteAsync(
        string name,
        IReadOnlyDictionary<string, JsonElement> input,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));
        _queue.Enqueue(new ToolJob(name, input, tcs));
        _event.Raise();
        return tcs.Task;
    }

    public Task<string> GetProjectContextAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));
        _queue.Enqueue(new GetContextJob(tcs));
        _event.Raise();
        return tcs.Task;
    }

    public Task<bool> FocusElementAsync(long elementId, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));
        _queue.Enqueue(new FocusElementJob(elementId, tcs));
        _event.Raise();
        return tcs.Task;
    }

    public void Execute(UIApplication app)
    {
        while (_queue.TryDequeue(out var job))
        {
            switch (job)
            {
                case BeginTurnJob b: HandleBeginTurn(app, b); break;
                case EndTurnJob e: HandleEndTurn(e); break;
                case ToolJob t: HandleTool(app, t); break;
                case GetContextJob g: HandleGetContext(app, g); break;
                case FocusElementJob f: HandleFocusElement(app, f); break;
            }
        }
    }

    private void HandleFocusElement(UIApplication app, FocusElementJob job)
    {
        try
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) { job.Tcs.TrySetResult(false); return; }
            var id = new ElementId(job.Id);
            var element = uidoc.Document.GetElement(id);
            if (element == null) { job.Tcs.TrySetResult(false); return; }
            uidoc.Selection.SetElementIds(new[] { id });
            uidoc.ShowElements(new[] { id });
            job.Tcs.TrySetResult(true);
        }
        catch (Exception ex) { job.Tcs.TrySetException(ex); }
    }

    private void HandleBeginTurn(UIApplication app, BeginTurnJob job)
    {
        try
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc != null && _activeGroup == null)
            {
                _activeGroup = new TransactionGroup(doc, job.Label);
                _activeGroup.Start();
            }
            job.Tcs.TrySetResult(true);
        }
        catch (Exception ex) { job.Tcs.TrySetException(ex); }
    }

    private void HandleEndTurn(EndTurnJob job)
    {
        try
        {
            if (_activeGroup != null)
            {
                if (_activeGroup.HasStarted() && !_activeGroup.HasEnded())
                    _activeGroup.Assimilate();
                _activeGroup.Dispose();
                _activeGroup = null;
            }
            job.Tcs.TrySetResult(true);
        }
        catch (Exception ex) { job.Tcs.TrySetException(ex); }
    }

    private void HandleTool(UIApplication app, ToolJob job)
    {
        try
        {
            var tool = _registry.Get(job.Name)
                ?? throw new InvalidOperationException($"Unknown tool: {job.Name}");

            string result;
            if (tool.RequiresTransaction)
            {
                var doc = app.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                using var tx = new Transaction(doc, $"Claude: {tool.Name}");
                tx.Start();
                try
                {
                    result = tool.Execute(job.Input, app);
                    tx.Commit();
                }
                catch
                {
                    tx.RollBack();
                    throw;
                }
            }
            else
            {
                result = tool.Execute(job.Input, app);
            }
            job.Tcs.TrySetResult(result);
        }
        catch (Exception ex) { job.Tcs.TrySetException(ex); }
    }

    private void HandleGetContext(UIApplication app, GetContextJob job)
    {
        try
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                job.Tcs.TrySetResult("(No document is currently open.)");
                return;
            }

            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.Elevation).Select(l => l.Name).ToList();

            string units;
            try
            {
                var fmt = doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
                units = fmt.GetUnitTypeId().TypeId;
            }
            catch { units = "(unknown)"; }

            var info = new
            {
                title = doc.Title,
                active_view = doc.ActiveView?.Name,
                length_units = units,
                level_count = levels.Count,
                levels
            };
            job.Tcs.TrySetResult(JsonSerializer.Serialize(info));
        }
        catch (Exception ex) { job.Tcs.TrySetException(ex); }
    }

    public string GetName() => "ClaudeRevit.ToolDispatcher";

    private abstract record Job;
    private sealed record BeginTurnJob(string Label, TaskCompletionSource<bool> Tcs) : Job;
    private sealed record EndTurnJob(TaskCompletionSource<bool> Tcs) : Job;
    private sealed record ToolJob(
        string Name,
        IReadOnlyDictionary<string, JsonElement> Input,
        TaskCompletionSource<string> Tcs) : Job;
    private sealed record GetContextJob(TaskCompletionSource<string> Tcs) : Job;
    private sealed record FocusElementJob(long Id, TaskCompletionSource<bool> Tcs) : Job;
}
