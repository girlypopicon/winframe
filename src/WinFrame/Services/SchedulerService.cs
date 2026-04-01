using System;
using System.Collections.Generic;
using System.Linq;
using WinFrame.Models;
using TaskStatus = WinFrame.Models.TaskStatus;

namespace WinFrame.Services;

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
