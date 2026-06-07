using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ClaudeRevit.UI;

/// <summary>
/// Lightweight markdown → WPF Inline converter, sized for chat content.
/// Supports: ``` fenced code, # / ## / ### headers, - / * bullets, 1. numbered,
/// **bold**, *italic*, `inline code`. Plain text passes through unchanged.
/// </summary>
public static class MarkdownInlineRenderer
{
    private static readonly FontFamily MonoFont = new("Consolas");
    private static readonly Brush CodeBg = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
    private static readonly Brush CodeBlockBg = new SolidColorBrush(Color.FromRgb(0xF6, 0xF8, 0xFA));

    public static IEnumerable<Inline> Render(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) yield break;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        bool firstLineEmitted = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("```"))
            {
                var sb = new System.Text.StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].StartsWith("```"))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(lines[i]);
                    i++;
                }
                if (firstLineEmitted) yield return new LineBreak();
                yield return new Run(sb.ToString())
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Background = CodeBlockBg
                };
                firstLineEmitted = true;
                continue;
            }

            if (firstLineEmitted) yield return new LineBreak();

            if (line.StartsWith("### "))
            {
                yield return new Run(line.Substring(4))
                {
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14
                };
            }
            else if (line.StartsWith("## "))
            {
                yield return new Run(line.Substring(3))
                {
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 15
                };
            }
            else if (line.StartsWith("# "))
            {
                yield return new Run(line.Substring(2))
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 16
                };
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                yield return new Run("• ");
                foreach (var inline in ParseInline(line.Substring(2))) yield return inline;
            }
            else if (Regex.IsMatch(line, @"^\d+\.\s"))
            {
                var idx = line.IndexOf('.');
                yield return new Run(line.Substring(0, idx + 2));
                foreach (var inline in ParseInline(line.Substring(idx + 2))) yield return inline;
            }
            else
            {
                foreach (var inline in ParseInline(line)) yield return inline;
            }

            firstLineEmitted = true;
        }
    }

    private static IEnumerable<Inline> ParseInline(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        int i = 0;
        var buf = new System.Text.StringBuilder();

        Inline FlushBuffer()
        {
            var s = buf.ToString();
            buf.Clear();
            return new Run(s);
        }

        while (i < text.Length)
        {
            // **bold**
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                int end = text.IndexOf("**", i + 2);
                if (end > i + 2)
                {
                    if (buf.Length > 0) yield return FlushBuffer();
                    yield return new Run(text.Substring(i + 2, end - i - 2))
                    {
                        FontWeight = FontWeights.Bold
                    };
                    i = end + 2;
                    continue;
                }
            }

            // *italic*
            if (text[i] == '*' && (i == 0 || text[i - 1] != '*'))
            {
                int end = text.IndexOf('*', i + 1);
                if (end > i + 1 && (end + 1 >= text.Length || text[end + 1] != '*'))
                {
                    if (buf.Length > 0) yield return FlushBuffer();
                    yield return new Run(text.Substring(i + 1, end - i - 1))
                    {
                        FontStyle = FontStyles.Italic
                    };
                    i = end + 1;
                    continue;
                }
            }

            // `code`
            if (text[i] == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    if (buf.Length > 0) yield return FlushBuffer();
                    yield return new Run(text.Substring(i + 1, end - i - 1))
                    {
                        FontFamily = MonoFont,
                        FontSize = 12,
                        Background = CodeBg
                    };
                    i = end + 1;
                    continue;
                }
            }

            buf.Append(text[i]);
            i++;
        }

        if (buf.Length > 0) yield return FlushBuffer();
    }
}
