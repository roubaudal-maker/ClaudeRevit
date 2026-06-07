using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public class GetActiveViewInfo : IRevitTool
{
    public string Name => "get_active_view_info";

    public string Description =>
        "Returns information about Revit's currently active view: name, view type, scale, " +
        "detail level, and whether it is a template. Use this whenever the user asks about " +
        "the current view or what they are looking at.";

    public InputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, JsonElement>(),
        Required = []
    };

    public bool RequiresTransaction => false;

    public string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return JsonSerializer.Serialize(new { error = "No document is open." });

        var view = doc.ActiveView;
        if (view == null)
            return JsonSerializer.Serialize(new { error = "No active view." });

        return JsonSerializer.Serialize(new
        {
            name = view.Name,
            view_type = view.ViewType.ToString(),
            scale = view.Scale,
            detail_level = view.DetailLevel.ToString(),
            is_template = view.IsTemplate,
            document_title = doc.Title
        });
    }
}
