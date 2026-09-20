using System.Windows.Controls;

namespace Viora.UI.Pages.Support;

public partial class FeedbackPage : UserControl
{
    public FeedbackPage(SupportViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
