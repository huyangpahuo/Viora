using System.Windows.Controls;

namespace Viora.UI.Pages.Legal;

public partial class LegalPage : UserControl
{
    public LegalPage(LegalViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}