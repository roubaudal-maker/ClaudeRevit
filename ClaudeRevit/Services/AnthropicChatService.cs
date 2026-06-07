using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Anthropic;
using Anthropic.Core;
using Anthropic.Helpers;
using Anthropic.Models.Beta.Messages;
using ClaudeRevit.Tools;
using ClaudeRevit.UI;
using ApiModel = Anthropic.Models.Messages.Model;
using ApiRole = Anthropic.Models.Beta.Messages.Role;

namespace ClaudeRevit.Services;

public class AnthropicChatService
{
    // The BaseSystemPrompt is intentionally large and static — it gets cached via prompt caching
    // so we don't re-process it every turn. Same goes for the tools list. Dynamic per-turn context
    // (current document + selection) is appended as a SECOND system block that is NOT cached.
    private const string BaseSystemPrompt =
        "You are Claude, integrated into Autodesk Revit 2027 as an AI assistant for architects and engineers. " +
        "You have tools to inspect AND modify the active model. Call them — don't narrate or ask permission.\n\n" +
        "UNITS: All spatial inputs to tools are in feet (Revit's internal unit). Convert from user-given units " +
        "before calling: 1 m ≈ 3.28084 ft, 1 mm ≈ 0.00328084 ft, 1 in ≈ 0.0833333 ft.\n\n" +
        "CONVENTIONS: x = east, y = north. Plan coordinates only — z comes from the level. When the user is " +
        "vague about position, place geometry near the origin and pick sensible defaults. When they say " +
        "'this' / 'these' / 'the selected', call get_selection first.\n\n" +
        "For destructive operations (delete_elements affecting many items, set_parameter on critical fields), " +
        "briefly confirm with the user before acting if intent is ambiguous. Otherwise just proceed.\n\n" +
        "If a tool returns an error, read it and adjust — try a different level name, fix coordinates, etc. " +
        "All edits within one user prompt are bundled into a single undo entry, so the user can ⌃Z to revert.";

    private const int MaxIterations = 8;

    private AnthropicClient? _client;

    public void RecreateClient() => _client = null;

    private AnthropicClient GetClient()
    {
        if (_client != null) return _client;
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "ANTHROPIC_API_KEY is not set. Click the gear icon in the chat pane to enter your key.");
        return _client = new AnthropicClient(new ClientOptions { ApiKey = apiKey });
    }

    public async Task SendAsync(
        ObservableCollection<ChatMessage> conversation,
        string model,
        CancellationToken ct = default)
    {
        var ui = Dispatcher.CurrentDispatcher;
        var client = GetClient();

        // Dynamic-per-turn context (current document + selection). NOT cached.
        var contextJson = await ToolDispatcher.Instance.GetProjectContextAsync(ct);
        var dynamicContext = "CURRENT DOCUMENT:\n" + contextJson;

        var sel = SelectionService.Current;
        if (sel.Ids.Count > 0)
        {
            var idList = sel.Ids.Count > 30
                ? string.Join(", ", sel.Ids.Take(30)) + $", … +{sel.Ids.Count - 30} more"
                : string.Join(", ", sel.Ids);
            dynamicContext += $"\n\nCURRENT SELECTION: {sel.Description}. Element IDs: [{idList}]";
        }

        // System content: BaseSystemPrompt (CACHED, 1h) + dynamic context (NOT cached).
        // Cache breakpoint sits at the end of the first block so Anthropic re-uses it across turns.
        var systemBlocks = new List<BetaTextBlockParam>
        {
            new BetaTextBlockParam
            {
                Text = BaseSystemPrompt,
                CacheControl = new BetaCacheControlEphemeral { Ttl = Ttl.Ttl1h }
            },
            new BetaTextBlockParam { Text = dynamicContext }
        };

        var apiMessages = new List<BetaMessageParam>();
        foreach (var m in conversation)
        {
            if (m.Role != "user" && m.Role != "assistant") continue;
            if (string.IsNullOrEmpty(m.Text)) continue;
            apiMessages.Add(new BetaMessageParam
            {
                Role = m.Role == "user" ? ApiRole.User : ApiRole.Assistant,
                Content = m.Text
            });
        }

        // Tools list is large (~47 tools with descriptions + schemas) and never changes.
        // Mark the LAST tool with a cache breakpoint so the whole tools array is cached together.
        var allTools = ToolRegistry.Instance.All.ToList();
        var toolDefs = new List<BetaTool>(allTools.Count);
        for (int i = 0; i < allTools.Count; i++)
        {
            var t = allTools[i];
            toolDefs.Add(new BetaTool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema,
                CacheControl = i == allTools.Count - 1
                    ? new BetaCacheControlEphemeral { Ttl = Ttl.Ttl1h }
                    : null
            });
        }
        var tools = toolDefs.Select(t => (BetaToolUnion)t).ToList();

        var lastUser = conversation.LastOrDefault(m => m.Role == "user")?.Text ?? "Claude turn";
        var turnLabel = "Claude: " + Truncate(lastUser, 60);

        await ToolDispatcher.Instance.BeginTurnAsync(turnLabel, ct);
        try
        {
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                var parameters = new MessageCreateParams
                {
                    Model = ResolveModel(model),
                    MaxTokens = 4096,
                    Messages = apiMessages,
                    System = systemBlocks,
                    Tools = tools
                };

                var aggregated = await StreamOneTurnAsync(client, parameters, conversation, ui, ct);

                // Cost tracking — including cache reads/writes
                try
                {
                    var u = aggregated.Usage;
                    long cacheRead = 0, cacheCreation = 0;
                    try { cacheRead = u.CacheReadInputTokens ?? 0; } catch { }
                    try { cacheCreation = u.CacheCreationInputTokens ?? 0; } catch { }
                    UsageTracker.Add(model, u.InputTokens, u.OutputTokens, cacheCreation, cacheRead);
                }
                catch { /* non-fatal */ }

                // Pull text + tool_use from aggregated content
                var aggregatedText = "";
                var toolUseBlocks = new List<(string Id, string Name, IReadOnlyDictionary<string, JsonElement> Input)>();
                foreach (var block in aggregated.Content)
                {
                    if (block.TryPickText(out var t))
                        aggregatedText += t.Text;
                    else if (block.TryPickToolUse(out var tu))
                        toolUseBlocks.Add((tu.ID, tu.Name, tu.Input));
                }

                if (toolUseBlocks.Count == 0) break;

                var assistantBlocks = new List<BetaContentBlockParam>();
                if (!string.IsNullOrEmpty(aggregatedText))
                    assistantBlocks.Add(new BetaTextBlockParam { Text = aggregatedText });

                var resultBlocks = new List<BetaContentBlockParam>();
                foreach (var (id, name, inp) in toolUseBlocks)
                {
                    assistantBlocks.Add(new BetaToolUseBlockParam
                    {
                        ID = id,
                        Name = name,
                        Input = inp
                    });

                    ChatMessage toolMsg = null!;
                    await ui.InvokeAsync(() =>
                    {
                        toolMsg = new ChatMessage
                        {
                            Role = "tool",
                            ToolName = name,
                            Text = FormatInput(inp) + "\n…running"
                        };
                        conversation.Add(toolMsg);
                    });

                    string content;
                    bool isError = false;
                    try
                    {
                        content = await ToolDispatcher.Instance.ExecuteAsync(name, inp, ct);
                        var display = FormatInput(inp) + "\n→ " + Truncate(content, 400);
                        await ui.InvokeAsync(() => toolMsg.Text = display);
                    }
                    catch (Exception ex)
                    {
                        content = $"Error: {ex.Message}";
                        isError = true;
                        var display = $"{FormatInput(inp)}\n✗ {ex.Message}";
                        await ui.InvokeAsync(() =>
                        {
                            toolMsg.Text = display;
                            toolMsg.IsError = true;
                        });
                    }

                    resultBlocks.Add(new BetaToolResultBlockParam
                    {
                        ToolUseID = id,
                        Content = content,
                        IsError = isError
                    });
                }

                apiMessages.Add(new BetaMessageParam { Role = ApiRole.Assistant, Content = assistantBlocks });
                apiMessages.Add(new BetaMessageParam { Role = ApiRole.User, Content = resultBlocks });
            }
        }
        finally
        {
            await ToolDispatcher.Instance.EndTurnAsync(CancellationToken.None);
        }
    }

    private async Task<BetaMessage> StreamOneTurnAsync(
        AnthropicClient client,
        MessageCreateParams parameters,
        ObservableCollection<ChatMessage> conversation,
        Dispatcher ui,
        CancellationToken ct)
    {
        var aggregator = new BetaMessageContentAggregator();
        var stream = client.Beta.Messages.CreateStreaming(parameters, ct);

        ChatMessage? assistantBubble = null;

        await foreach (var ev in stream.CollectAsync(aggregator).WithCancellation(ct))
        {
            if (ev.TryPickContentBlockDelta(out var bd) &&
                bd.Delta.TryPickText(out var td) &&
                !string.IsNullOrEmpty(td.Text))
            {
                if (assistantBubble == null)
                {
                    var bubble = new ChatMessage { Role = "assistant", Text = "" };
                    assistantBubble = bubble;
                    await ui.InvokeAsync(() => conversation.Add(bubble));
                }
                var append = td.Text;
                var existing = assistantBubble;
                await ui.InvokeAsync(() => existing.Text += append);
            }
        }

        return aggregator.Message();
    }

    private static ApiModel ResolveModel(string model) => model switch
    {
        "opus-4-7" => ApiModel.ClaudeOpus4_7,
        "haiku-4-5" => ApiModel.ClaudeHaiku4_5,
        _ => ApiModel.ClaudeSonnet4_6
    };

    private static string FormatInput(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (input.Count == 0) return "(no arguments)";
        try { return JsonSerializer.Serialize(input, new JsonSerializerOptions { WriteIndented = false }); }
        catch { return "(unprintable input)"; }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + $"… ({s.Length - max} more chars)";
}
