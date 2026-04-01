using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinFrame.ViewModels;

namespace WinFrame.Views;

public partial class ChatView : UserControl
{
    private MainViewModel? _viewModel;
    private INotifyCollectionChanged? _subscribedMessages;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    // ── DataContext wiring ──────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = e.NewValue as MainViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ResubscribeToMessages();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedThread))
            ResubscribeToMessages();
    }

    // ── Message collection subscription ────────────────────────────────────

    private void ResubscribeToMessages()
    {
        if (_subscribedMessages is not null)
            _subscribedMessages.CollectionChanged -= OnMessagesCollectionChanged;

        _subscribedMessages = _viewModel?.SelectedThread?.Messages;

        if (_subscribedMessages is not null)
        {
            _subscribedMessages.CollectionChanged += OnMessagesCollectionChanged;
            // Scroll to the bottom whenever the thread switches so the user
            // always sees the most recent message.
            ScrollToBottom();
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            ScrollToBottom();
    }

    // ── Scroll helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Schedules a scroll-to-bottom at Background priority so WPF has time to
    /// measure and render the newly added items before we move the offset.
    /// </summary>
    private void ScrollToBottom()
    {
        Dispatcher.InvokeAsync(
            () => MessagesScrollViewer.ScrollToBottom(),
            DispatcherPriority.Background);
    }

    // ── Cleanup ─────────────────────────────────────────────────────────────

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (_subscribedMessages is not null)
            _subscribedMessages.CollectionChanged -= OnMessagesCollectionChanged;

        _viewModel = null;
        _subscribedMessages = null;
    }

    // ── Input handling ──────────────────────────────────────────────────────

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is MainViewModel vm && vm.SendMessageCommand.CanExecute(null))
                vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }
}
