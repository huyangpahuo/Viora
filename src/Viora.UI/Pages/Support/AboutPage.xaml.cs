using System.Windows.Controls;

namespace Viora.UI.Pages.Support;

public partial class AboutPage : UserControl
{
    public AboutPage(SupportViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
