using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ClaudeRevit.Tools;

namespace ClaudeRevit.UI;

/// <summary>
/// Attached property for TextBlock or RichTextBox that scans the bound text for sequences of
/// 4-9 digits (likely Revit element ids) and renders them as Hyperlink Inlines. Clicking a
/// hyperlink selects+zooms-to that element via ToolDispatcher.FocusElementAsync.
/// </summary>
public static class ElementIdLinker
{
    private static readonly Regex IdRegex = new(
        @"(?<![.\d])\d{4,9}(?![.\d])",
        RegexOptions.Compiled);

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(ElementIdLinker),
            new PropertyMetadata(null, OnTextChanged));

    public static void SetText(DependencyObject obj, string value) =>
        obj.SetValue(TextProperty, value);

    public static string GetText(DependencyObject obj) =>
        (string)obj.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = (string?)e.NewValue ?? "";
        var inlines = BuildInlines(text);

        if (d is RichTextBox rtb)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            foreach (var inline in inlines) paragraph.Inlines.Add(inline);
            rtb.Document = new FlowDocument(paragraph) { PagePadding = new Thickness(0) };
            return;
        }

        if (d is TextBlock tb)
        {
            tb.Inlines.Clear();
            foreach (var inline in inlines) tb.Inlines.Add(inline);
        }
    }

    private static List<Inline> BuildInlines(string text)
    {
        var result = new List<Inline>();
        if (text.Length == 0) return result;

        int lastEnd = 0;
        foreach (Match m in IdRegex.Matches(text))
        {
            if (m.Index > lastEnd)
                result.Add(new Run(text.Substring(lastEnd, m.Index - lastEnd)));

            var idStr = m.Value;
            var hl = new Hyperlink(new Run(idStr))
            {
                Cursor = Cursors.Hand,
                ToolTip = "Select and zoom to this element in Revit"
            };
            hl.Click += (_, _) =>
            {
                if (long.TryParse(idStr, out var id))
                    _ = ToolDispatcher.Instance.FocusElementAsync(id);
            };
            result.Add(hl);

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            result.Add(new Run(text.Substring(lastEnd)));

        return result;
    }
}
