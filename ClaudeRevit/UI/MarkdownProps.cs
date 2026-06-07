using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ClaudeRevit.UI;

public static class MarkdownProps
{
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.RegisterAttached(
            "Markdown",
            typeof(string),
            typeof(MarkdownProps),
            new PropertyMetadata(null, OnMarkdownChanged));

    public static void SetMarkdown(DependencyObject obj, string value) =>
        obj.SetValue(MarkdownProperty, value);

    public static string GetMarkdown(DependencyObject obj) =>
        (string)obj.GetValue(MarkdownProperty);

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = (string?)e.NewValue ?? "";
        var rendered = MarkdownInlineRenderer.Render(text).ToList();

        if (d is RichTextBox rtb)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            foreach (var inline in rendered) paragraph.Inlines.Add(inline);
            rtb.Document = new FlowDocument(paragraph) { PagePadding = new Thickness(0) };
            return;
        }

        if (d is TextBlock tb)
        {
            tb.Inlines.Clear();
            foreach (var inline in rendered) tb.Inlines.Add(inline);
        }
    }
}
