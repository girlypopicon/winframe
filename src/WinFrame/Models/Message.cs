using System;

namespace WinFrame.Models;

public enum MessageRole { User, Assistant, System }

public class Message
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public MessageRole Role { get; init; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsStreaming { get; set; } = false;
}
