using System.Windows.Controls;

namespace Viora.UI.Pages.Sponsor;

public partial class SponsorPage : UserControl
{
    public SponsorPage(SponsorViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}