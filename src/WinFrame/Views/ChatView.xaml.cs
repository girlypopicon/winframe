using System.Windows.Controls;
using System.Windows.Input;

namespace WinFrame.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is WinFrame.ViewModels.MainViewModel vm)
            {
                if (vm.SendMessageCommand.CanExecute(null))
                    vm.SendMessageCommand.Execute(null);
            }
            e.Handled = true;
        }
    }
}
