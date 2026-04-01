using System;
using System.Collections.ObjectModel;
using WinFrame.Models;

namespace WinFrame.ViewModels;

public class ThreadViewModel : BaseViewModel
{
    private readonly ConversationThread _thread;
    private bool _isSelected;
    private bool _hasUnread;

    public ThreadViewModel(ConversationThread thread)
    {
        _thread = thread;
        foreach (var msg in thread.Messages)
            Messages.Add(new MessageViewModel(msg));
    }

    public Guid Id => _thread.Id;
    public string Title
    {
        get => _thread.Title;
        set
        {
            _thread.Title = value;
            OnPropertyChanged();
        }
    }

    public bool IsOnboarding => _thread.IsOnboarding;
    public bool IsPinned => _thread.IsPinned;
    public DateTime CreatedAt => _thread.CreatedAt;
    public DateTime LastActivity => _thread.LastActivity;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasUnread
    {
        get => _hasUnread;
        set => SetProperty(ref _hasUnread, value);
    }

    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    public void AddMessage(Message message)
    {
        _thread.Messages.Add(message);
        var vm = new MessageViewModel(message);
        Messages.Add(vm);
        _thread.LastActivity = DateTime.UtcNow;
        OnPropertyChanged(nameof(LastActivity));
    }

    public Message? GetLastMessage() =>
        _thread.Messages.Count > 0 ? _thread.Messages[^1] : null;
}
