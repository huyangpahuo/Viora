using System.Windows.Controls;

namespace Viora.UI.Pages.Community;

public partial class CommunityPage : UserControl
{
    public CommunityPage(CommunityViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}