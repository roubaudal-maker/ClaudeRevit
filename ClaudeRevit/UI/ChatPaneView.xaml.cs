using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClaudeRevit.Services;

namespace ClaudeRevit.UI;

public partial class ChatPaneView : UserControl
{
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    private readonly AnthropicChatService _service = new();
    private CancellationTokenSource? _cts;
    private string _selectedModel = "sonnet-4-6";

    public ChatPaneView()
    {
        InitializeComponent();
        DataContext = this;
        Messages.CollectionChanged += OnMessagesChanged;

        foreach (var m in HistoryStore.Load())
            Messages.Add(m);

        UsageTracker.Updated += UpdateUsageText;
        UpdateUsageText();

        SelectionService.Changed += OnSelectionChanged;
        OnSelectionChanged(SelectionService.Current);
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (ChatMessage m in e.NewItems)
                m.PropertyChanged += (_, _) => ScheduleScroll();
        }
        ScheduleScroll();
    }

    private void ScheduleScroll() =>
        Dispatcher.BeginInvoke(new Action(() => MessagesScroll.ScrollToBottom()), DispatcherPriority.Background);

    private void UpdateUsageText() =>
        Dispatcher.BeginInvoke(new Action(() => UsageText.Text = UsageTracker.Format()));

    private void OnSelectionChanged(SelectionService.SelectionInfo info)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (info.Ids.Count == 0)
            {
                SelectionPillBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                SelectionPillText.Text = "Selected: " + info.Description;
                SelectionPillBorder.Visibility = Visibility.Visible;
            }
        }));
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (_cts != null) _cts.Cancel();
            else _ = SendAsync();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) _cts.Cancel();
        else _ = SendAsync();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) return;
        Messages.Clear();
        HistoryStore.Clear();
        UsageTracker.Reset();
        StatusText.Text = "";
        InputBox.Focus();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            _service.RecreateClient();
            StatusText.Text = "API key updated.";
        }
    }

    private void ModelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelPicker.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _selectedModel = tag;
    }

    private async Task SendAsync()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || _cts != null) return;

        InputBox.Text = "";
        SendButton.Content = "Cancel";
        StatusText.Text = "Sending...";

        Messages.Add(new ChatMessage { Role = "user", Text = text });

        _cts = new CancellationTokenSource();
        try
        {
            await _service.SendAsync(Messages, _selectedModel, _cts.Token);
            StatusText.Text = "";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage { Role = "assistant", Text = $"[Error: {ex.Message}]" });
            StatusText.Text = "Error";
        }
        finally
        {
            HistoryStore.Save(Messages);
            _cts?.Dispose();
            _cts = null;
            SendButton.Content = "Send";
            InputBox.Focus();
        }
    }
}
