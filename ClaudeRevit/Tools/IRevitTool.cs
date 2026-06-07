using System.Collections.Generic;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Autodesk.Revit.UI;

namespace ClaudeRevit.Tools;

public interface IRevitTool
{
    string Name { get; }
    string Description { get; }
    InputSchema InputSchema { get; }
    bool RequiresTransaction { get; }
    string Execute(IReadOnlyDictionary<string, JsonElement> input, UIApplication app);
}
