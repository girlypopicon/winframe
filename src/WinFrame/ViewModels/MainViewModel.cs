using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WinFrame.Models;
using WinFrame.Services;

namespace WinFrame.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class MainViewModel : BaseViewModel
{
    private readonly ThreadManager _threadManager;
    private readonly GitHubAuthService _authService;
    private readonly CopilotService _copilotService;

    private ThreadViewModel? _selectedThread;
    private bool _isAuthenticated;
    private bool _isLoading;
    private string _inputText = string.Empty;
    private string _statusText = string.Empty;
    private CancellationTokenSource? _sendCts;

    // Login flow state
    private bool _isDeviceFlowActive;
    private string _deviceCode = string.Empty;
    private string _statusMessage = string.Empty;
    private CancellationTokenSource? _loginCts;

    public ObservableCollection<ThreadViewModel> Threads { get; } = new();
    public ThreadViewModel OnboardingThread { get; private set; } = null!;

    public ThreadViewModel? SelectedThread
    {
        get => _selectedThread;
        set
        {
            if (_selectedThread != null) _selectedThread.IsSelected = false;
            SetProperty(ref _selectedThread, value);
            if (_selectedThread != null) _selectedThread.IsSelected = true;
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            SetProperty(ref _isLoading, value);
            (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsDeviceFlowActive
    {
        get => _isDeviceFlowActive;
        set => SetProperty(ref _isDeviceFlowActive, value);
    }

    public string DeviceCode
    {
        get => _deviceCode;
        set => SetProperty(ref _deviceCode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public GitHubUser? CurrentUser => _authService.CurrentUser;

    public ICommand SendMessageCommand { get; }
    public ICommand NewThreadCommand { get; }
    public ICommand SelectThreadCommand { get; }
    public ICommand CloseThreadCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand StartLoginCommand { get; }
    public ICommand CancelLoginCommand { get; }

    public MainViewModel(ThreadManager threadManager, GitHubAuthService authService, CopilotService copilotService)
    {
        _threadManager = threadManager;
        _authService = authService;
        _copilotService = copilotService;

        SendMessageCommand = new RelayCommand(
            _ => _ = SendMessageAsync(),
            _ => !IsLoading && !string.IsNullOrWhiteSpace(InputText));

        NewThreadCommand = new RelayCommand(_ => CreateNewThread());
        SelectThreadCommand = new RelayCommand(param =>
        {
            if (param is ThreadViewModel tvm) SelectedThread = tvm;
        });
        CloseThreadCommand = new RelayCommand(param =>
        {
            if (param is ThreadViewModel tvm) CloseThread(tvm.Id);
        });
        LoginCommand = new RelayCommand(_ => _ = StartLoginFlowAsync());
        LogoutCommand = new RelayCommand(_ => Logout());
        CancelCommand = new RelayCommand(_ => CancelSend());
        StartLoginCommand = new RelayCommand(_ => _ = StartLoginFlowAsync());
        CancelLoginCommand = new RelayCommand(_ => CancelLogin());

        _authService.AuthenticationChanged += OnAuthenticationChanged;

        InitializeThreads();
        IsAuthenticated = _authService.IsAuthenticated;
    }

    private void InitializeThreads()
    {
        foreach (var thread in _threadManager.Threads)
        {
            var vm = new ThreadViewModel(thread);
            Threads.Add(vm);
            if (thread.IsOnboarding)
            {
                OnboardingThread = vm;
                SelectedThread = vm;
            }
        }

        _threadManager.ThreadCreated += (_, thread) =>
        {
            var vm = new ThreadViewModel(thread);
            Threads.Add(vm);
            SelectedThread = vm;
        };

        _threadManager.ThreadClosed += (_, id) =>
        {
            var vm = Threads.FirstOrDefault(t => t.Id == id);
            if (vm != null)
            {
                Threads.Remove(vm);
                if (SelectedThread?.Id == id)
                    SelectedThread = OnboardingThread;
            }
        };
    }

    private void CreateNewThread()
    {
        var count = Threads.Count(t => !t.IsOnboarding);
        _threadManager.CreateThread($"New Thread {count + 1}");
    }

    private void CloseThread(Guid id)
    {
        _threadManager.CloseThread(id);
    }

    private async Task SendMessageAsync()
    {
        if (SelectedThread == null || string.IsNullOrWhiteSpace(InputText)) return;

        var userMessage = new Message
        {
            Role = MessageRole.User,
            Content = InputText.Trim()
        };
        SelectedThread.AddMessage(userMessage);
        InputText = string.Empty;
        IsLoading = true;
        StatusText = "Thinking...";

        var assistantMessage = new Message
        {
            Role = MessageRole.Assistant,
            Content = string.Empty,
            IsStreaming = true
        };
        SelectedThread.AddMessage(assistantMessage);
        var assistantVm = SelectedThread.Messages.Last();

        _sendCts = new CancellationTokenSource();
        try
        {
            var thread = _threadManager.GetThread(SelectedThread.Id);
            var messages = thread?.Messages.Where(m => m.Role != MessageRole.System)
                                           .ToList() ?? new();
            var contextMessages = messages.Where(m => !m.IsStreaming || m.Content.Length > 0).ToList();

            await foreach (var token in _copilotService.SendMessageAsync(contextMessages, _sendCts.Token))
            {
                assistantMessage.Content += token;
                assistantVm.Content = assistantMessage.Content;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            assistantMessage.Content = $"Error: {ex.Message}";
            assistantVm.Content = assistantMessage.Content;
        }
        finally
        {
            assistantMessage.IsStreaming = false;
            IsLoading = false;
            StatusText = string.Empty;
            _sendCts?.Dispose();
            _sendCts = null;
        }
    }

    private void CancelSend()
    {
        _sendCts?.Cancel();
    }

    private async Task StartLoginFlowAsync()
    {
        if (IsDeviceFlowActive) return;

        _loginCts = new CancellationTokenSource();
        IsDeviceFlowActive = true;
        StatusMessage = "Starting authorization...";
        DeviceCode = string.Empty;

        try
        {
            var result = await _authService.StartDeviceFlowAsync(_loginCts.Token);
            DeviceCode = result.UserCode;
            StatusMessage = "Waiting for authorization...";

            // Open browser
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result.VerificationUri,
                    UseShellExecute = true
                });
            }
            catch { }

            var success = await _authService.PollForTokenAsync(
                result.DeviceCode, result.Interval, _loginCts.Token);

            if (!success)
                StatusMessage = "Authorization failed or expired. Please try again.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsDeviceFlowActive = false;
            _loginCts?.Dispose();
            _loginCts = null;
        }
    }

    private void CancelLogin()
    {
        _loginCts?.Cancel();
        IsDeviceFlowActive = false;
    }

    private void Logout()
    {
        _authService.Logout();
    }

    private void OnAuthenticationChanged(object? sender, EventArgs e)
    {
        IsAuthenticated = _authService.IsAuthenticated;
        OnPropertyChanged(nameof(CurrentUser));
        if (IsAuthenticated)
        {
            IsDeviceFlowActive = false;
        }
    }
}
