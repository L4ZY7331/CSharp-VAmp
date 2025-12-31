using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CSharpVAmp.ViewModels;

namespace CSharpVAmp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void InstanceBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is InstanceBoxViewModel viewModel)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                viewModel.OnControlLeftClick();
                e.Handled = true;
            }
            else
            {
                viewModel.OnLeftClick();
                e.Handled = true;
            }
        }
    }

    private void InstanceBox_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is InstanceBoxViewModel viewModel)
        {
            viewModel.OnRightClick();
        }
    }
}

