using System.Windows.Controls;

namespace Viora.UI.Pages.Support;

public partial class HelpPage : UserControl
{
    public HelpPage(SupportViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
