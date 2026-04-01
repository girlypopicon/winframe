using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace WinFrame.Tests;

// Inline model/service duplicates to avoid WPF dependencies

public enum ThreadType { Onboarding, Temporary }
public enum MessageRole { User, Assistant, System }
public enum TaskStatus { Pending, InProgress, Completed, Cancelled }

public class Message
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public MessageRole Role { get; init; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsStreaming { get; set; } = false;
}

public class ConversationThread
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public ThreadType Type { get; init; } = ThreadType.Temporary;
    public bool IsPinned => Type == ThreadType.Onboarding;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public List<Message> Messages { get; } = new();
    public bool IsOnboarding => Type == ThreadType.Onboarding;
}

public class ScheduledTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ThreadId { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTime? ScheduledAt { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

// Inline ThreadManager
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
            Content = "Welcome to WinFrame!",
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
}

// Inline SchedulerService
public class SchedulerService
{
    private readonly List<ScheduledTask> _tasks = new();

    public event EventHandler? TasksChanged;

    public void AddTask(ScheduledTask task)
    {
        _tasks.Add(task);
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CompleteTask(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null) return false;
        task.Status = TaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        TasksChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool CancelTask(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null) return false;
        task.Status = TaskStatus.Cancelled;
        TasksChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public IReadOnlyList<ScheduledTask> GetPendingTasks(Guid threadId) =>
        _tasks.Where(t => t.ThreadId == threadId && t.Status == TaskStatus.Pending)
              .OrderBy(t => t.ScheduledAt ?? t.CreatedAt)
              .ToList();

    public IReadOnlyList<ScheduledTask> GetAllPendingTasks() =>
        _tasks.Where(t => t.Status == TaskStatus.Pending)
              .OrderBy(t => t.ScheduledAt ?? t.CreatedAt)
              .ToList();

    public IReadOnlyList<ScheduledTask> GetTasksForThread(Guid threadId) =>
        _tasks.Where(t => t.ThreadId == threadId)
              .OrderByDescending(t => t.CreatedAt)
              .ToList();
}

// Tests
public class ThreadManagerTests
{
    [Fact]
    public void Constructor_CreatesOnboardingThread()
    {
        var manager = new ThreadManager();
        Assert.Single(manager.Threads);
        Assert.True(manager.Threads[0].IsOnboarding);
        Assert.Equal("Main Thread", manager.Threads[0].Title);
    }

    [Fact]
    public void Constructor_OnboardingThreadHasWelcomeMessage()
    {
        var manager = new ThreadManager();
        Assert.NotEmpty(manager.OnboardingThread.Messages);
        Assert.Equal(MessageRole.Assistant, manager.OnboardingThread.Messages[0].Role);
    }

    [Fact]
    public void CreateThread_AddsToThreads()
    {
        var manager = new ThreadManager();
        var thread = manager.CreateThread("Test Thread");
        Assert.Equal(2, manager.Threads.Count);
        Assert.Equal("Test Thread", thread.Title);
        Assert.False(thread.IsOnboarding);
    }

    [Fact]
    public void CreateThread_EmptyTitle_UsesDefault()
    {
        var manager = new ThreadManager();
        var thread = manager.CreateThread("");
        Assert.False(string.IsNullOrWhiteSpace(thread.Title));
    }

    [Fact]
    public void CreateThread_RaisesThreadCreatedEvent()
    {
        var manager = new ThreadManager();
        ConversationThread? createdThread = null;
        manager.ThreadCreated += (_, t) => createdThread = t;
        var thread = manager.CreateThread("Event Test");
        Assert.NotNull(createdThread);
        Assert.Equal(thread.Id, createdThread!.Id);
    }

    [Fact]
    public void CloseThread_RemovesFromThreads()
    {
        var manager = new ThreadManager();
        var thread = manager.CreateThread("To Close");
        Assert.Equal(2, manager.Threads.Count);
        var result = manager.CloseThread(thread.Id);
        Assert.True(result);
        Assert.Single(manager.Threads);
    }

    [Fact]
    public void CloseThread_OnboardingThread_ReturnsFalse()
    {
        var manager = new ThreadManager();
        var result = manager.CloseThread(manager.OnboardingThread.Id);
        Assert.False(result);
        Assert.Single(manager.Threads);
    }

    [Fact]
    public void CloseThread_NonExistent_ReturnsFalse()
    {
        var manager = new ThreadManager();
        var result = manager.CloseThread(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public void CloseThread_RaisesThreadClosedEvent()
    {
        var manager = new ThreadManager();
        var thread = manager.CreateThread("Event Close");
        Guid closedId = Guid.Empty;
        manager.ThreadClosed += (_, id) => closedId = id;
        manager.CloseThread(thread.Id);
        Assert.Equal(thread.Id, closedId);
    }

    [Fact]
    public void GetThread_ReturnsCorrectThread()
    {
        var manager = new ThreadManager();
        var thread = manager.CreateThread("Find Me");
        var found = manager.GetThread(thread.Id);
        Assert.NotNull(found);
        Assert.Equal("Find Me", found!.Title);
    }

    [Fact]
    public void GetThread_NonExistent_ReturnsNull()
    {
        var manager = new ThreadManager();
        var found = manager.GetThread(Guid.NewGuid());
        Assert.Null(found);
    }
}

public class SchedulerServiceTests
{
    [Fact]
    public void AddTask_IncreasesTaskCount()
    {
        var service = new SchedulerService();
        var threadId = Guid.NewGuid();
        service.AddTask(new ScheduledTask { ThreadId = threadId, Title = "Task 1" });
        var tasks = service.GetTasksForThread(threadId);
        Assert.Single(tasks);
    }

    [Fact]
    public void AddTask_RaisesTasksChangedEvent()
    {
        var service = new SchedulerService();
        bool eventRaised = false;
        service.TasksChanged += (_, _) => eventRaised = true;
        service.AddTask(new ScheduledTask { Title = "Test" });
        Assert.True(eventRaised);
    }

    [Fact]
    public void CompleteTask_SetsStatusToCompleted()
    {
        var service = new SchedulerService();
        var task = new ScheduledTask { Title = "Complete Me" };
        service.AddTask(task);
        var result = service.CompleteTask(task.Id);
        Assert.True(result);
        Assert.Equal(TaskStatus.Completed, task.Status);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public void CompleteTask_NonExistent_ReturnsFalse()
    {
        var service = new SchedulerService();
        Assert.False(service.CompleteTask(Guid.NewGuid()));
    }

    [Fact]
    public void CancelTask_SetsStatusToCancelled()
    {
        var service = new SchedulerService();
        var task = new ScheduledTask { Title = "Cancel Me" };
        service.AddTask(task);
        var result = service.CancelTask(task.Id);
        Assert.True(result);
        Assert.Equal(TaskStatus.Cancelled, task.Status);
    }

    [Fact]
    public void GetPendingTasks_ReturnsOnlyPending()
    {
        var service = new SchedulerService();
        var threadId = Guid.NewGuid();
        var pending = new ScheduledTask { ThreadId = threadId, Title = "Pending" };
        var completed = new ScheduledTask { ThreadId = threadId, Title = "Completed" };
        service.AddTask(pending);
        service.AddTask(completed);
        service.CompleteTask(completed.Id);

        var result = service.GetPendingTasks(threadId);
        Assert.Single(result);
        Assert.Equal("Pending", result[0].Title);
    }

    [Fact]
    public void GetAllPendingTasks_ReturnsAllPendingAcrossThreads()
    {
        var service = new SchedulerService();
        service.AddTask(new ScheduledTask { ThreadId = Guid.NewGuid(), Title = "T1" });
        service.AddTask(new ScheduledTask { ThreadId = Guid.NewGuid(), Title = "T2" });
        var result = service.GetAllPendingTasks();
        Assert.Equal(2, result.Count);
    }
}
