using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ClaudeRevit.UI;

/// <summary>
/// Attached property that sets plain text on a RichTextBox as a single Paragraph/Run.
/// Used for user message bubbles where we want selectable text but no markdown parsing.
/// </summary>
public static class PlainTextProp
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(PlainTextProp),
            new PropertyMetadata(null, OnTextChanged));

    public static void SetText(DependencyObject obj, string value) =>
        obj.SetValue(TextProperty, value);

    public static string GetText(DependencyObject obj) =>
        (string)obj.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox rtb) return;
        var text = (string?)e.NewValue ?? "";
        var paragraph = new Paragraph(new Run(text)) { Margin = new Thickness(0) };
        rtb.Document = new FlowDocument(paragraph) { PagePadding = new Thickness(0) };
    }
}
