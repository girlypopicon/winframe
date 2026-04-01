using System;

namespace WinFrame.Models;

public enum TaskStatus { Pending, InProgress, Completed, Cancelled }

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
