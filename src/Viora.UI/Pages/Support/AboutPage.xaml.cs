using System.Windows.Controls;

namespace Viora.UI.Pages.Support;

public partial class AboutPage : UserControl
{
    public AboutPage(SupportViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnQrBackdropClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ((SupportViewModel)DataContext).CloseQrCommand.Execute(null);
}
