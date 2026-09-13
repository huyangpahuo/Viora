using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Viora.UI.Shell;

namespace Viora.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ShellViewModel>();
    }
}
