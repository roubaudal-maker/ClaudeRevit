using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClaudeRevit.UI;

public class ChatMessage : INotifyPropertyChanged
{
    private string _text = "";
    private bool _isError;

    public string Role { get; init; } = "user";
    public string? ToolName { get; init; }

    public string RoleDisplay => Role switch
    {
        "user" => "You",
        "assistant" => "Claude",
        "tool" => $"🔧 {ToolName}",
        _ => Role
    };

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public bool IsError
    {
        get => _isError;
        set
        {
            if (_isError == value) return;
            _isError = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
