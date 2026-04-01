using System;
using WinFrame.Models;

namespace WinFrame.ViewModels;

public class MessageViewModel : BaseViewModel
{
    private readonly Message _message;
    private string _content;

    public MessageViewModel(Message message)
    {
        _message = message;
        _content = message.Content;
    }

    public string Content
    {
        get => _content;
        set
        {
            _message.Content = value;
            SetProperty(ref _content, value);
        }
    }

    public MessageRole Role => _message.Role;
    public DateTime Timestamp => _message.Timestamp;
    public bool IsUser => _message.Role == MessageRole.User;
    public bool IsAssistant => _message.Role == MessageRole.Assistant;
    public bool IsSystem => _message.Role == MessageRole.System;
    public bool IsStreaming => _message.IsStreaming;

    public string RoleLabel => _message.Role switch
    {
        MessageRole.User => "You",
        MessageRole.Assistant => "Copilot",
        MessageRole.System => "System",
        _ => string.Empty
    };

    public string TimeDisplay => _message.Timestamp.ToLocalTime().ToString("HH:mm");
}
