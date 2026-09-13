using System.Windows.Controls;

namespace Viora.UI.Pages.Help;

public partial class HelpPage : UserControl
{
    public HelpPage(HelpViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}