# winframe

A native Windows AI coding assistant inspired by [OpenAI Codex](https://github.com/openai/codex). WinFrame lets you run focused AI conversations through GitHub Copilot, organised as threads — with a permanent **Main Thread** that ties everything together.

---

## Features

- **Multi-thread conversations** — create as many temporary threads as you need
- **Permanent Main Thread** — always present (★), can never be closed; acts as the onboarding hub and overview
- **GitHub OAuth login** — sign in with your GitHub account using the Device Authorization flow (no redirect URI needed)
- **GitHub Copilot AI** — chat completions via the Copilot Chat API (`api.githubcopilot.com`)
- **Scheduling & tasks** — attach tasks and reminders to any thread
- **Dark Codex-inspired UI** — custom titlebar, dark theme, smooth WPF controls

---

## Requirements

- Windows 10 / 11 (64-bit)
- .NET 9 Desktop Runtime (or SDK for development)
- A GitHub account with an active **GitHub Copilot** subscription

---

## Getting Started

### 1. Register a GitHub OAuth App

1. Go to **GitHub → Settings → Developer settings → OAuth Apps → New OAuth App**
2. Set **Application name**: `WinFrame`
3. Set **Homepage URL**: `https://github.com/girlypopicon/winframe`
4. Set **Authorization callback URL**: `http://localhost` (not used for device flow, but required by GitHub)
5. Click **Register application** and copy the **Client ID**

### 2. Configure the Client ID

Edit `src/WinFrame/appsettings.json`:

```json
{
  "GitHub": {
    "ClientId": "<YOUR_GITHUB_OAUTH_APP_CLIENT_ID>",
    "Scope": "read:user copilot"
  }
}
```

> **Note**: Never commit real credentials to source control. For development use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets):
> ```
> cd src/WinFrame
> dotnet user-secrets set "GitHub:ClientId" "<your-client-id>"
> ```

### 3. Build & Run

```bash
dotnet build WinFrame.sln
dotnet run --project src/WinFrame
```

---

## Project Structure

```
WinFrame.sln
src/
  WinFrame/
    Models/          ConversationThread, Message, GitHubUser, ScheduledTask
    Services/        GitHubAuthService, CopilotService, ThreadManager, SchedulerService
    ViewModels/      MainViewModel, ThreadViewModel, MessageViewModel, BaseViewModel
    Views/           MainWindow, ChatView, ThreadListView, LoginView
    Converters/      BoolToVisibilityConverter, MessageAlignmentConverter
    App.xaml         Global dark-theme resource dictionary
    appsettings.json GitHub OAuth configuration (replace placeholder before use)
tests/
  WinFrame.Tests/    18 xUnit tests for ThreadManager and SchedulerService
```

---

## Architecture

- **MVVM** with `INotifyPropertyChanged` base and `RelayCommand`
- **Dependency Injection** via `Microsoft.Extensions.DependencyInjection`
- **GitHub Device Authorization Flow** — polls `github.com/login/oauth/access_token` until the user completes browser authorization
- **Copilot Chat API** — SSE streaming over `api.githubcopilot.com/chat/completions`; falls back to a mock response when unauthenticated

---

## Running Tests

```bash
dotnet test tests/WinFrame.Tests
```
