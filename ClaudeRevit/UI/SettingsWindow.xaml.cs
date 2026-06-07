using System;
using System.Windows;

namespace ClaudeRevit.UI;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var existing = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrEmpty(existing)) ApiKeyBox.Password = existing;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show(this,
                "API key cannot be empty.",
                "Claude Revit",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", key, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", key);

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
