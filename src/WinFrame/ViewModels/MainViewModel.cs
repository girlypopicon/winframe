using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WinFrame.Models;
using WinFrame.Services;

namespace WinFrame.ViewModels;

public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly ThreadManager _threadManager;
    private readonly GitHubAuthService _authService;
    private readonly CopilotService _copilotService;

    // Captured once on the UI thread so event callbacks can safely post back.
    private readonly SynchronizationContext _syncContext;

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

    private bool _disposed;

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
        set
        {
            if (SetProperty(ref _inputText, value))
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
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
        // Must be created on the UI thread so the SynchronizationContext is the
        // WPF dispatcher context, enabling safe marshalling of event callbacks.
        _syncContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException(
                $"{nameof(MainViewModel)} must be created on the UI thread.");

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

        _threadManager.ThreadCreated += OnThreadCreated;
        _threadManager.ThreadClosed += OnThreadClosed;
    }

    // Handler stored as a named method so it can be unsubscribed in Dispose().
    private void OnThreadCreated(object? sender, ConversationThread thread)
    {
        _syncContext.Post(_ =>
        {
            var vm = new ThreadViewModel(thread);
            Threads.Add(vm);
            SelectedThread = vm;
        }, null);
    }

    // Handler stored as a named method so it can be unsubscribed in Dispose().
    private void OnThreadClosed(object? sender, Guid id)
    {
        _syncContext.Post(_ =>
        {
            var vm = Threads.FirstOrDefault(t => t.Id == id);
            if (vm != null)
            {
                Threads.Remove(vm);
                if (SelectedThread?.Id == id)
                    SelectedThread = OnboardingThread;
            }
        }, null);
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
        // Capture the MessageViewModel for the assistant bubble so we can
        // update Content and IsStreaming incrementally on the UI thread.
        var assistantVm = SelectedThread.Messages.Last();

        // Create a fresh CTS for this send operation.
        var cts = new CancellationTokenSource();
        _sendCts = cts;

        try
        {
            var thread = _threadManager.GetThread(SelectedThread.Id);
            var messages = thread?.Messages.Where(m => m.Role != MessageRole.System)
                                           .ToList() ?? new();
            var contextMessages = messages.Where(m => !m.IsStreaming || m.Content.Length > 0).ToList();

            await foreach (var token in _copilotService.SendMessageAsync(contextMessages, cts.Token))
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
            // Mark streaming as finished on both the model and the view-model so
            // any IsStreaming binding updates correctly.
            assistantVm.IsStreaming = false;
            IsLoading = false;
            StatusText = string.Empty;

            // Null out _sendCts before disposing so CancelSend() cannot call
            // Cancel() on an already-disposed instance.
            _sendCts = null;
            cts.Dispose();
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

            // Open the browser so the user can complete the device flow.
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result.VerificationUri,
                    UseShellExecute = true
                });
            }
            catch { /* browser launch is best-effort */ }

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
            IsDeviceFlowActive = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe all event handlers to prevent memory/reference leaks.
        _authService.AuthenticationChanged -= OnAuthenticationChanged;
        _threadManager.ThreadCreated -= OnThreadCreated;
        _threadManager.ThreadClosed -= OnThreadClosed;

        // Cancel and clean up any in-flight async operations.
        _sendCts?.Cancel();
        _sendCts?.Dispose();
        _sendCts = null;

        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _loginCts = null;
    }
}
