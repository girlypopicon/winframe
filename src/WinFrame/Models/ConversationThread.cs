using System;
using System.Collections.Generic;

namespace WinFrame.Models;

public enum ThreadType { Onboarding, Temporary }

public class ConversationThread
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public ThreadType Type { get; init; } = ThreadType.Temporary;
    public bool IsPinned => Type == ThreadType.Onboarding;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public List<Message> Messages { get; } = new();
    public List<ScheduledTask> Tasks { get; } = new();
    public bool IsOnboarding => Type == ThreadType.Onboarding;
}
