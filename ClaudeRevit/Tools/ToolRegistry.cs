using System.Collections.Generic;
using System.Linq;
using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;

namespace ClaudeRevit.Tools;

public class ToolRegistry
{
    private static ToolRegistry? _instance;
    public static ToolRegistry Instance => _instance ??= new ToolRegistry();

    private readonly Dictionary<string, IRevitTool> _tools = new();

    public void Register(IRevitTool tool) => _tools[tool.Name] = tool;

    public IRevitTool? Get(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    public IReadOnlyCollection<IRevitTool> All => _tools.Values;

    public IReadOnlyList<BetaTool> BuildToolDefinitions() =>
        _tools.Values.Select(t => new BetaTool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema
        }).ToList();
}
