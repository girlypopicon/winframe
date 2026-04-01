using System;
using WinFrame.Models;

namespace WinFrame.ViewModels;

public class MessageViewModel : BaseViewModel
{
    private readonly Message _message;
    private string _content;
    private bool _isStreaming;

    public MessageViewModel(Message message)
    {
        _message = message;
        _content = message.Content;
        _isStreaming = message.IsStreaming;
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

    /// <summary>
    /// Gets or sets whether this message is still being streamed from the API.
    /// Setting this to <c>false</c> hides the streaming indicator in the UI.
    /// </summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            if (SetProperty(ref _isStreaming, value))
                _message.IsStreaming = value;
        }
    }

    public MessageRole Role => _message.Role;
    public DateTime Timestamp => _message.Timestamp;
    public bool IsUser => _message.Role == MessageRole.User;
    public bool IsAssistant => _message.Role == MessageRole.Assistant;
    public bool IsSystem => _message.Role == MessageRole.System;

    public string RoleLabel => _message.Role switch
    {
        MessageRole.User => "You",
        MessageRole.Assistant => "Copilot",
        MessageRole.System => "System",
        _ => string.Empty
    };

    public string TimeDisplay => _message.Timestamp.ToLocalTime().ToString("HH:mm");
}
