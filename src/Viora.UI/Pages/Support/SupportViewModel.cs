using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Viora.UI.Services;

namespace Viora.UI.Pages.Support;

/// <summary>社区/外链配置:群链接后续由官网确定后填入,空 = 按钮显示"即将上线"。</summary>
public static class SupportLinks
{
    public const string GitHub = "https://github.com/huyangpahuo/Viora";
    public const string PluginRepo = "https://github.com/huyangpahuo/Viora-plugins";

    // 社区群邀请链接
    public const string QQGroup = "https://qm.qq.com/cgi-bin/qm/qr?k=1057438076";
    public const string Discord = "https://discord.gg/GRThdfrUjt";
    public const string Telegram = "https://t.me/+MEjpv8FLI9IzYTA1";

    public static bool HasQQ => !string.IsNullOrEmpty(QQGroup);
    public static bool HasDiscord => !string.IsNullOrEmpty(Discord);
    public static bool HasTelegram => !string.IsNullOrEmpty(Telegram);
}

/// <summary>外链按钮行。</summary>
public sealed class LinkItemViewModel
{
    public LinkItemViewModel(string titleKey, string? url, string iconKey, string? subtitle = null,
        string? qrResource = null)
    {
        TitleKey = titleKey;
        Url = url;
        IconKey = iconKey;
        Subtitle = subtitle;
        QrResource = qrResource;
    }

    public string TitleKey { get; }

    public string? Url { get; }

    public string IconKey { get; }

    public string? Subtitle { get; }

    /// <summary>内置二维码图片的 pack URI(如关于页 QQ/TG);null = 无。</summary>
    public string? QrResource { get; }

    public bool IsAvailable => !string.IsNullOrEmpty(Url);

    public bool HasQr => QrResource is not null;
}

/// <summary>关于/帮助/反馈三个页面共享的 VM(链接、诊断信息、日志操作)。</summary>
public partial class SupportViewModel : ObservableObject
{
    private readonly IUiAlert _alert;
    private readonly Core.Settings.ISettingsService _settings;

    public SupportViewModel(IUiAlert alert, Core.Settings.ISettingsService settings)
    {
        _alert = alert;
        _settings = settings;
    }

    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            return version is null ? "1.0" : version.ToString(3);
        }
    }

    public string DiagnosticInfo
    {
        get
        {
            var os = Environment.OSVersion.VersionString;
            return $"Viora {AppVersion} | {os} | {Environment.ProcessPath}";
        }
    }

    // ---------- 关于页:社区入口 ----------

    public ObservableCollection<LinkItemViewModel> CommunityLinks { get; } = new()
    {
        new("Support.Community.QQ", SupportLinks.QQGroup, "Icon.Brand.QQ",
            qrResource: "pack://application:,,,/Viora;component/assets/QQ.jpg"),
        new("Support.Community.Discord", SupportLinks.Discord, "Icon.Brand.Discord"),
        new("Support.Community.Telegram", SupportLinks.Telegram, "Icon.Brand.Telegram",
            qrResource: "pack://application:,,,/Viora;component/assets/Telegram.png"),
        new("Support.Community.GitHub", SupportLinks.GitHub, "Icon.Brand.GitHub"),
    };

    // ---------- 帮助页:快捷入口 ----------

    public ObservableCollection<LinkItemViewModel> HelpLinks { get; } = new()
    {
        new("Support.Help.PluginRepo", SupportLinks.PluginRepo, "Icon.PuzzlePiece", "Support.Help.PluginRepo.Sub"),
        new("Support.Community.GitHub", SupportLinks.GitHub, "Icon.Gear"),
    };

    // ---------- 关于页:插件发布步骤 ----------

    public sealed record StepItem(int Index, string Text);

    public IReadOnlyList<StepItem> PluginSteps { get; } =
    [
        new(1, "Support.Step1"),
        new(2, "Support.Step2"),
        new(3, "Support.Step3"),
        new(4, "Support.Step4"),
        new(5, "Support.Step5"),
    ];

    // ---------- 帮助页:FAQ ----------

    public sealed record FaqItem(string Question, string Answer);

    public IReadOnlyList<FaqItem> Faqs { get; } =
    [
        new("Support.Faq1.Q", "Support.Faq1.A"),
        new("Support.Faq2.Q", "Support.Faq2.A"),
        new("Support.Faq3.Q", "Support.Faq3.A"),
        new("Support.Faq4.Q", "Support.Faq4.A"),
    ];

    [RelayCommand]
    private void OpenPluginRepo()
    {
        try
        {
            Process.Start(new ProcessStartInfo(SupportLinks.PluginRepo) { UseShellExecute = true });
        }
        catch
        {
            // 无默认浏览器等极端情况:静默
        }
    }

    // ---------- 反馈页 ----------

    public ObservableCollection<LinkItemViewModel> FeedbackLinks { get; } = new()
    {
        new("Support.Feedback.Issues", $"{SupportLinks.GitHub}/issues", "Icon.Brand.GitHub"),
        new("Support.Community.QQ", SupportLinks.QQGroup, "Icon.Brand.QQ"),
        new("Support.Community.Discord", SupportLinks.Discord, "Icon.Brand.Discord"),
        new("Support.Community.Telegram", SupportLinks.Telegram, "Icon.Brand.Telegram"),
    };

    [RelayCommand]
    private void OpenLink(LinkItemViewModel? link)
    {
        if (link is null) return;
        if (string.IsNullOrEmpty(link.Url))
        {
            _alert.Info(Viora.UI.Localization.Tr.Get("Support.ComingSoon"));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
        }
        catch
        {
            // 无默认浏览器等极端情况:静默
        }
    }

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _qrImage;

    [ObservableProperty]
    private string _qrTitle = string.Empty;

    [ObservableProperty]
    private bool _isQrOpen;

    [RelayCommand]
    private void ShowQr(LinkItemViewModel? link)
    {
        if (link?.QrResource is null) return;
        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(link.QrResource);
        image.EndInit();
        image.Freeze();
        QrImage = image;
        QrTitle = Viora.UI.Localization.Tr.Get(link.TitleKey);
        IsQrOpen = true;
    }

    [RelayCommand]
    private void CloseQr() => IsQrOpen = false;

    [RelayCommand]
    private void CopyDiagnostics()
    {
        try
        {
            System.Windows.Clipboard.SetText(DiagnosticInfo);
            _alert.Info(Viora.UI.Localization.Tr.Get("Support.Feedback.Copied"));
        }
        catch
        {
            // 剪贴板被占用等:静默
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            var logs = Viora.Core.AppLocations.ResolveLogsFolder(_settings.Current.Debug.LogFolder);
            Directory.CreateDirectory(logs);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logs}\"") { UseShellExecute = true });
        }
        catch
        {
            // 打开失败:静默
        }
    }
}
