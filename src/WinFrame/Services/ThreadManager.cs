using System;
using System.Collections.ObjectModel;
using System.Linq;
using WinFrame.Models;

namespace WinFrame.Services;

public class ThreadManager
{
    public ObservableCollection<ConversationThread> Threads { get; } = new();
    public ConversationThread OnboardingThread { get; }

    public event EventHandler<ConversationThread>? ThreadCreated;
    public event EventHandler<Guid>? ThreadClosed;

    public ThreadManager()
    {
        OnboardingThread = new ConversationThread
        {
            Title = "Main Thread",
            Type = ThreadType.Onboarding,
        };

        OnboardingThread.Messages.Add(new Message
        {
            Role = MessageRole.Assistant,
            Content = "Welcome to WinFrame! 👋\n\nThis is your Main Thread — it's always here and can never be closed. Use it to:\n• Get an overview of all your threads\n• Track tasks and reminders\n• Access quick commands\n\nCreate new threads using the + button in the sidebar to start focused conversations.",
        });

        Threads.Add(OnboardingThread);
    }

    public ConversationThread CreateThread(string title)
    {
        var thread = new ConversationThread
        {
            Title = string.IsNullOrWhiteSpace(title) ? $"Thread {Threads.Count}" : title,
            Type = ThreadType.Temporary,
        };
        Threads.Add(thread);
        ThreadCreated?.Invoke(this, thread);
        return thread;
    }

    public bool CloseThread(Guid id)
    {
        var thread = Threads.FirstOrDefault(t => t.Id == id);
        if (thread == null || thread.IsOnboarding) return false;
        Threads.Remove(thread);
        ThreadClosed?.Invoke(this, id);
        return true;
    }

    public ConversationThread? GetThread(Guid id) =>
        Threads.FirstOrDefault(t => t.Id == id);

    public ConversationThread GetActiveThread() =>
        Threads.FirstOrDefault(t => t.IsOnboarding) ?? Threads.First();
}
