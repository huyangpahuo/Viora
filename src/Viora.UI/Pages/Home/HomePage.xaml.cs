using System.Windows.Controls;

namespace Viora.UI.Pages.Home;

public partial class HomePage : UserControl
{
    public HomePage(HomeViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}