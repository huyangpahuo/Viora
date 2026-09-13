using System.Windows.Controls;

namespace Viora.UI.Pages.Feedback;

public partial class FeedbackPage : UserControl
{
    public FeedbackPage(FeedbackViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}