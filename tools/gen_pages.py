# -*- coding: utf-8 -*-
"""Generates the static Viora pages (Home/About/Help/Feedback/Community/Sponsor/Legal)."""
import os

BASE = os.path.join(os.path.dirname(__file__), '..', 'src', 'Viora.UI', 'Pages')

pages = {}

# ============ Home ============
pages['Home/HomePage'] = (r'''<UserControl
    x:Class="Viora.UI.Pages.Home.HomePage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:loc="clr-namespace:Viora.UI.Localization;assembly=Viora.UI"
    xmlns:pages="clr-namespace:Viora.UI.Pages;assembly=Viora.UI">
    <pages:PageChrome TitleKey="Home.Title" SubtitleKey="Home.Subtitle">
        <StackPanel MaxWidth="860">
            <StackPanel Orientation="Horizontal" Margin="0,8,0,24">
                <Button Style="{StaticResource VioraButton}" Content="{loc:Translate Home.Cta.Convert}" Command="{Binding GoConvertCommand}" Margin="0,0,12,0" />
                <Button Style="{StaticResource SecondaryButton}" Content="{loc:Translate Home.Cta.Plugins}" Command="{Binding GoPluginsCommand}" />
            </StackPanel>

            <TextBlock Style="{StaticResource Typography.Heading}" Text="{loc:Translate Home.FeaturesTitle}" Margin="0,0,0,12" />
            <UniformGrid Columns="3">
                <Border Style="{StaticResource VioraCard}" Margin="0,0,12,0">
                    <StackPanel>
                        <TextBlock Text="&#xE8E5;" FontFamily="Segoe MDL2 Assets" FontSize="22" Foreground="{StaticResource Color.Accent}" />
                        <TextBlock Style="{StaticResource Typography.Heading}" FontSize="14" Text="{loc:Translate Home.Feature.Convert.Title}" Margin="0,10,0,6" />
                        <TextBlock Style="{StaticResource Typography.BodySecondary}" TextWrapping="Wrap" Text="{loc:Translate Home.Feature.Convert.Body}" />
                    </StackPanel>
                </Border>
                <Border Style="{StaticResource VioraCard}" Margin="0,0,12,0">
                    <StackPanel>
                        <TextBlock Text="&#xE116;" FontFamily="Segoe MDL2 Assets" FontSize="22" Foreground="{StaticResource Color.Accent}" />
                        <TextBlock Style="{StaticResource Typography.Heading}" FontSize="14" Text="{loc:Translate Home.Feature.Plugins.Title}" Margin="0,10,0,6" />
                        <TextBlock Style="{StaticResource Typography.BodySecondary}" TextWrapping="Wrap" Text="{loc:Translate Home.Feature.Plugins.Body}" />
                    </StackPanel>
                </Border>
                <Border Style="{StaticResource VioraCard}">
                    <StackPanel>
                        <TextBlock Text="&#xE72E;" FontFamily="Segoe MDL2 Assets" FontSize="22" Foreground="{StaticResource Color.Accent}" />
                        <TextBlock Style="{StaticResource Typography.Heading}" FontSize="14" Text="{loc:Translate Home.Feature.Privacy.Title}" Margin="0,10,0,6" />
                        <TextBlock Style="{StaticResource Typography.BodySecondary}" TextWrapping="Wrap" Text="{loc:Translate Home.Feature.Privacy.Body}" />
                    </StackPanel>
                </Border>
            </UniformGrid>

            <TextBlock Style="{StaticResource Typography.Heading}" Text="{loc:Translate Home.RecentTitle}" Margin="0,24,0,12" />
            <Border Style="{StaticResource VioraCard}" Padding="24">
                <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{loc:Translate Home.RecentEmpty}" TextWrapping="Wrap" />
            </Border>
        </StackPanel>
    </pages:PageChrome>
</UserControl>
''', r'''using System.Windows.Controls;

namespace Viora.UI.Pages.Home;

public partial class HomePage : UserControl
{
    public HomePage(HomeViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}''')

# ============ About ============
pages['About/AboutPage'] = (r'''<UserControl
    x:Class="Viora.UI.Pages.About.AboutPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:loc="clr-namespace:Viora.UI.Localization;assembly=Viora.UI"
    xmlns:pages="clr-namespace:Viora.UI.Pages;assembly=Viora.UI">
    <pages:PageChrome TitleKey="About.Title">
        <StackPanel MaxWidth="720">
            <StackPanel Orientation="Horizontal" Margin="0,8,0,4">
                <Border Width="44" Height="44" Background="{StaticResource Color.Accent}" CornerRadius="12">
                    <TextBlock Text="V" FontSize="24" FontWeight="Bold" Foreground="{StaticResource Color.Text.OnAccent}" HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <StackPanel Margin="14,0,0,0" VerticalAlignment="Center">
                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="22" Text="Viora" />
                    <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{Binding VersionText}" />
                </StackPanel>
            </StackPanel>

            <TextBlock Style="{StaticResource Typography.Body}" Text="{loc:Translate About.Description}" TextWrapping="Wrap" Margin="0,16,0,8" />
            <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{loc:Translate About.Author}" />

            <StackPanel Orientation="Horizontal" Margin="0,16,0,0">
                <Button Style="{StaticResource LinkButton}" Content="{loc:Translate About.ProjectLink}" Command="{Binding OpenProjectCommand}" Margin="0,0,24,0" />
                <Button Style="{StaticResource SecondaryButton}" Content="{Binding UpdateButtonText}" Command="{Binding CheckUpdatesCommand}" />
            </StackPanel>
            <TextBlock Style="{StaticResource Typography.Caption}" Text="{Binding UpdateStatusText}" Margin="0,8,0,0" TextWrapping="Wrap" />

            <TextBlock Style="{StaticResource Typography.Heading}" Text="{loc:Translate About.ThirdParty}" Margin="0,24,0,8" />
            <Border Style="{StaticResource VioraCard}" Padding="20">
                <ItemsControl ItemsSource="{Binding ThirdPartyComponents}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Style="{StaticResource Typography.BodySecondary}" Margin="0,2"
                                       Text="{Binding Display, Mode=OneWay}" />
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Border>

            <TextBlock Style="{StaticResource Typography.Caption}" Text="{loc:Translate About.License}" Margin="0,16,0,0" />
        </StackPanel>
    </pages:PageChrome>
</UserControl>
''', r'''using System.Windows.Controls;

namespace Viora.UI.Pages.About;

public partial class AboutPage : UserControl
{
    public AboutPage(AboutViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}''')

# ============ Help ============
pages['Help/HelpPage'] = (r'''<UserControl
    x:Class="Viora.UI.Pages.Help.HelpPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:loc="clr-namespace:Viora.UI.Localization;assembly=Viora.UI"
    xmlns:pages="clr-namespace:Viora.UI.Pages;assembly=Viora.UI">
    <pages:PageChrome TitleKey="Help.Title" SubtitleKey="Help.Subtitle">
        <StackPanel MaxWidth="720">
            <Border Style="{StaticResource VioraCard}" Margin="0,8,0,16">
                <StackPanel>
                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="15" Text="{loc:Translate Help.GettingStarted.Title}" />
                    <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{loc:Translate Help.GettingStarted.Body}" TextWrapping="Wrap" Margin="0,6,0,0" />
                </StackPanel>
            </Border>
            <UniformGrid Columns="2">
                <Button Style="{StaticResource SecondaryButton}" Content="{loc:Translate Help.Docs}" Command="{Binding OpenDocsCommand}" Margin="0,0,12,12" Height="44" />
                <Button Style="{StaticResource SecondaryButton}" Content="{loc:Translate Help.Faq}" Command="{Binding OpenFaqCommand}" Margin="0,0,0,12" Height="44" />
                <Button Style="{StaticResource SecondaryButton}" Content="{loc:Translate Help.GitHubDocs}" Command="{Binding OpenWikiCommand}" Margin="0,0,12,0" Height="44" />
                <Button Style="{StaticResource SecondaryButton}" Content="{loc:Translate Help.OfficialSite}" Command="{Binding OpenSiteCommand}" Margin="0,0,0,0" Height="44" />
            </UniformGrid>
            <TextBlock Style="{StaticResource Typography.Caption}" Text="{loc:Translate Help.InApp.Future}" Margin="0,8,0,0" />
            <Button Style="{StaticResource LinkButton}" Content="{loc:Translate Settings.Debug.OpenLogs}" Command="{Binding OpenLogsCommand}" HorizontalAlignment="Left" Margin="0,16,0,0" />
        </StackPanel>
    </pages:PageChrome>
</UserControl>
''', r'''using System.Windows.Controls;

namespace Viora.UI.Pages.Help;

public partial class HelpPage : UserControl
{
    public HelpPage(HelpViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}''')

# ============ Feedback ============
pages['Feedback/FeedbackPage'] = (r'''<UserControl
    x:Class="Viora.UI.Pages.Feedback.FeedbackPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:loc="clr-namespace:Viora.UI.Localization;assembly=Viora.UI"
    xmlns:pages="clr-namespace:Viora.UI.Pages;assembly=Viora.UI">
    <pages:PageChrome TitleKey="Feedback.Title" SubtitleKey="Feedback.Subtitle">
        <StackPanel MaxWidth="760">
            <Grid Margin="0,8,0,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="12" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="12" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <Button Grid.Column="0" Command="{Binding OpenBugCommand}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="Card" Background="{StaticResource Color.Surface}" BorderBrush="{StaticResource Color.Border}" BorderThickness="1" CornerRadius="10" Padding="20" Cursor="Hand">
                                <StackPanel>
                                    <TextBlock Text="&#xE9F5;" FontFamily="Segoe MDL2 Assets" FontSize="20" Foreground="{StaticResource Color.Danger}" />
                                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="14" Text="{loc:Translate Feedback.Bug}" Margin="0,10,0,4" />
                                    <TextBlock Style="{StaticResource Typography.BodySecondary}" TextWrapping="Wrap" Text="{loc:Translate Feedback.Bug.Description}" />
                                </StackPanel>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="Card" Property="Background" Value="{StaticResource Color.SurfaceElevated}" />
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
                <Button Grid.Column="2" Command="{Binding OpenFeatureCommand}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="Card" Background="{StaticResource Color.Surface}" BorderBrush="{StaticResource Color.Border}" BorderThickness="1" CornerRadius="10" Padding="20" Cursor="Hand">
                                <StackPanel>
                                    <TextBlock Text="&#xE718;" FontFamily="Segoe MDL2 Assets" FontSize="20" Foreground="{StaticResource Color.Success}" />
                                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="14" Text="{loc:Translate Feedback.Feature}" Margin="0,10,0,4" />
                                    <TextBlock Style="{StaticResource Typography.BodySecondary}" TextWrapping="Wrap" Text="{loc:Translate Feedback.Feature.Description}" />
                                </StackPanel>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="Card" Property="Background" Value="{StaticResource Color.SurfaceElevated}" />
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
                <Button Grid.Column="4" Command="{Binding OpenGeneralCommand}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="Card" Background="{StaticResource Color.Surface}" BorderBrush="{StaticResource Color.Border}" BorderThickness="1" CornerRadius="10" Padding="20" Cursor="Hand">
                                <StackPanel>
                                    <TextBlock Text="&#xE77B;" FontFamily="Segoe MDL2 Assets" FontSize="20" Foreground="{StaticResource Color.Accent}" />
                                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="14" Text="{loc:Translate Feedback.General}" Margin="0,10,0,4" />
                                    <TextBlock Style="{StaticResource Typography.BodySecondary}" TextWrapping="Wrap" Text="{loc:Translate Feedback.General.Description}" />
                                </StackPanel>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="Card" Property="Background" Value="{StaticResource Color.SurfaceElevated}" />
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </Grid>
            <TextBlock Style="{StaticResource Typography.Caption}" Text="{loc:Translate Feedback.OpenIssues}" Margin="0,12,0,0" />
            <TextBlock Style="{StaticResource Typography.Caption}" Text="{loc:Translate Feedback.IncludeLogs}" Margin="0,4,0,0" TextWrapping="Wrap" />
        </StackPanel>
    </pages:PageChrome>
</UserControl>
''', r'''using System.Windows.Controls;

namespace Viora.UI.Pages.Feedback;

public partial class FeedbackPage : UserControl
{
    public FeedbackPage(FeedbackViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}''')

# ============ Community ============
pages['Community/CommunityPage'] = (r'''<UserControl
    x:Class="Viora.UI.Pages.Community.CommunityPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:loc="clr-namespace:Viora.UI.Localization;assembly=Viora.UI"
    xmlns:pages="clr-namespace:Viora.UI.Pages;assembly=Viora.UI">
    <pages:PageChrome TitleKey="Community.Title" SubtitleKey="Community.Subtitle">
        <StackPanel MaxWidth="720">
            <ItemsControl ItemsSource="{Binding Destinations}" Margin="0,8,0,0">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Style="{StaticResource VioraCard}" Padding="20" Margin="0,0,0,12">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel>
                                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="15" Text="{Binding Name}" />
                                    <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{Binding Description}" TextWrapping="Wrap" Margin="0,4,0,0" />
                                </StackPanel>
                                <Button Grid.Column="1" Style="{StaticResource SecondaryButton}" Content="{loc:Translate Community.Join}"
                                        Command="{Binding DataContext.JoinCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                        CommandParameter="{Binding}" VerticalAlignment="Center" />
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
            <TextBlock Style="{StaticResource Typography.Caption}" Text="{loc:Translate Community.Empty}" Margin="0,4,0,0"
                       Visibility="{Binding HasNoDestinations, Converter={StaticResource BoolToVisibilityConverter}}" />
        </StackPanel>
    </pages:PageChrome>
</UserControl>
''', r'''using System.Windows.Controls;

namespace Viora.UI.Pages.Community;

public partial class CommunityPage : UserControl
{
    public CommunityPage(CommunityViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}''')

# ============ Sponsor ============
pages['Sponsor/SponsorPage'] = (r'''<UserControl
    x:Class="Viora.UI.Pages.Sponsor.SponsorPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:loc="clr-namespace:Viora.UI.Localization;assembly=Viora.UI"
    xmlns:pages="clr-namespace:Viora.UI.Pages;assembly=Viora.UI">
    <pages:PageChrome TitleKey="Sponsor.Title" SubtitleKey="Sponsor.Subtitle">
        <StackPanel MaxWidth="720">
            <Border Style="{StaticResource VioraCard}" Margin="0,8,0,12">
                <StackPanel>
                    <TextBlock Style="{StaticResource Typography.Body}" Text="{loc:Translate Sponsor.Body}" TextWrapping="Wrap" />
                    <Button Style="{StaticResource VioraButton}" Content="{loc:Translate Sponsor.Button}" Command="{Binding OpenSponsorCommand}" HorizontalAlignment="Left" Margin="0,16,0,0" />
                </StackPanel>
            </Border>
            <TextBlock Style="{StaticResource Typography.Caption}" Text="{loc:Translate Sponsor.Thanks}" />
        </StackPanel>
    </pages:PageChrome>
</UserControl>
''', r'''using System.Windows.Controls;

namespace Viora.UI.Pages.Sponsor;

public partial class SponsorPage : UserControl
{
    public SponsorPage(SponsorViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}''')

# ============ Legal ============
pages['Legal/LegalPage'] = (r'''<UserControl
    x:Class="Viora.UI.Pages.Legal.LegalPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:loc="clr-namespace:Viora.UI.Localization;assembly=Viora.UI"
    xmlns:pages="clr-namespace:Viora.UI.Pages;assembly=Viora.UI">
    <pages:PageChrome TitleKey="Legal.Title" SubtitleKey="Legal.Subtitle">
        <StackPanel MaxWidth="760">
            <Border Style="{StaticResource VioraCard}" Margin="0,8,0,12">
                <StackPanel>
                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="15" Text="{loc:Translate Legal.UserAgreement}" />
                    <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{loc:Translate Legal.UserAgreement.Body}" TextWrapping="Wrap" Margin="0,6,0,0" />
                </StackPanel>
            </Border>
            <Border Style="{StaticResource VioraCard}" Margin="0,0,0,12">
                <StackPanel>
                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="15" Text="{loc:Translate Legal.PrivacyPolicy}" />
                    <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{loc:Translate Legal.PrivacyPolicy.Body}" TextWrapping="Wrap" Margin="0,6,0,0" />
                </StackPanel>
            </Border>
            <Border Style="{StaticResource VioraCard}" Margin="0,0,0,12">
                <StackPanel>
                    <TextBlock Style="{StaticResource Typography.Heading}" FontSize="15" Text="{loc:Translate Legal.OssLicenses}" />
                    <TextBlock Style="{StaticResource Typography.BodySecondary}" Text="{loc:Translate Legal.OssLicenses.Body}" TextWrapping="Wrap" Margin="0,6,0,8" />
                    <ItemsControl ItemsSource="{Binding Components}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Style="{StaticResource Typography.BodySecondary}" Margin="0,2" Text="{Binding Display}" />
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>
            </Border>
        </StackPanel>
    </pages:PageChrome>
</UserControl>
''', r'''using System.Windows.Controls;

namespace Viora.UI.Pages.Legal;

public partial class LegalPage : UserControl
{
    public LegalPage(LegalViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}''')

for path, (xaml, cs) in pages.items():
    folder = os.path.join(BASE, os.path.dirname(path))
    os.makedirs(folder, exist_ok=True)
    open(os.path.join(BASE, path + '.xaml'), 'w', encoding='utf-8').write(xaml)
    open(os.path.join(BASE, path + '.xaml.cs'), 'w', encoding='utf-8').write(cs)
print('wrote', len(pages), 'page pairs')
