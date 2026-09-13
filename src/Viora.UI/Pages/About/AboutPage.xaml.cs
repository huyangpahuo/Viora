using System.Windows.Controls;

namespace Viora.UI.Pages.About;

public partial class AboutPage : UserControl
{
    public AboutPage(AboutViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}