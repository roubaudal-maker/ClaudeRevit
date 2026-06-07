using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClaudeRevit.UI;

namespace ClaudeRevit.Services;

public static class HistoryStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeRevit", "conversation.json");

    public static void Save(IEnumerable<ChatMessage> messages)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            var dtos = messages.Select(m => new MessageDto(m.Role, m.ToolName, m.Text, m.IsError)).ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dtos));
        }
        catch { /* best-effort */ }
    }

    public static List<ChatMessage> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            var dtos = JsonSerializer.Deserialize<List<MessageDto>>(json) ?? new();
            return dtos.Select(d => new ChatMessage
            {
                Role = d.Role,
                ToolName = d.ToolName,
                Text = d.Text,
                IsError = d.IsError
            }).ToList();
        }
        catch
        {
            return new();
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch { }
    }

    private sealed record MessageDto(string Role, string? ToolName, string Text, bool IsError);
}
