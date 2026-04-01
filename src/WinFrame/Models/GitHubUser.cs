namespace WinFrame.Models;

public class GitHubUser
{
    public int Id { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool HasCopilotAccess { get; set; }
}
