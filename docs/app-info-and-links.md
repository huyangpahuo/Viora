# 应用信息与链接配置指南

本文回答一个问题:**软件的对外信息(官网、赞助、仓库、版本、图标、名称)分别在哪里修改?**

## 1. 所有对外链接 —— `src/Viora.UI/AppLinks.cs`(唯一入口)

「关于 / 帮助 / 反馈 / 社区 / 赞助 / 法律信息」页面的全部链接都取自这个静态类,
改一处即可全应用生效:

```csharp
public static class AppLinks
{
    public const string Repository    = "https://github.com/huyangpahuo/Viora"; // 仓库主页
    public const string OfficialSite  = Repository;  // 官方网站:有独立域名后改这里
    public const string Releases      = $"{Repository}/releases"; // 发布页(检查更新跳转)
    public const string Sponsors      = "https://github.com/sponsors/huyangpahuo"; // 赞助页
    public const string BugReport     = $"{Repository}/issues/new?template=bug_report.md";
    public const string FeatureRequest= $"{Repository}/issues/new?template=feature_request.md";
    public const string Discussions   = $"{Repository}/discussions"; // 社区讨论区
    public const string Wiki          = $"{Repository}/wiki";        // 在线文档
    public const string WikiFaq       = $"{Wiki}/FAQ";               // 常见问题
}
```

| 想改什么 | 改哪个字段 | 影响的页面 |
|---|---|---|
| 官方网站 | `OfficialSite` | 帮助 → 官方网站 |
| 赞助渠道 | `Sponsors` | 赞助页按钮 |
| 项目主页 | `Repository` | 关于 → GitHub 项目;法律信息 |
| 反馈入口 | `BugReport` / `FeatureRequest` / `Discussions` | 反馈页三个卡片 |
| 文档与 FAQ | `Wiki` / `WikiFaq` | 帮助页 |

> 开通 GitHub Sponsors 后,把 `Sponsors` 换成 `https://github.com/sponsors/<你的用户名>`。

## 2. 版本号 —— `src/Viora.App/Viora.App.csproj`

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
```

「关于」页显示的版本、插件兼容性检查(`HostVersion.Current`)都读自入口程序集版本。
发新版时同步更新这三项。

## 3. 应用名称与标语 —— 语言包

`src/Viora.Localization/Assets/zh-Hans.json` 与 `en.json`:

```json
"App.Name": "Viora",
"App.Tagline": "创作 · 转换 · 风格化",
```

侧边栏品牌区、窗口标题读取这两个键。所有界面文案同样在这两个文件里。

## 4. 图标与 Logo

| 位置 | 文件 / 配置 |
|---|---|
| 图标源图 | 仓库根目录 `枫原万叶.png`(64×64,临时 logo) |
| 构建用副本 | `assets/logo.png`(侧边栏品牌位、关于页) |
| 应用图标 | `assets/viora.ico`(16–256 多尺寸,由 `tools/gen_icon.ps1` 生成) |
| exe / 桌面快捷方式 / 资源管理器 | `Viora.App.csproj` 的 `<ApplicationIcon>` |
| 窗口标题栏 / 任务栏(运行中) | `MainWindow.xaml` 的 `Icon="pack://application:,,,/Viora.App;component/viora.ico"` |
| 应用内品牌位 / 关于页 | `ShellView.xaml`、`AboutPage.xaml` 引用 pack URI `logo.png` |

**换正式 logo 的步骤:**

1. 用新的方形 PNG(建议 ≥256×256)覆盖源图;
2. `powershell -NoProfile -File tools/gen_icon.ps1 <新图路径>` 重新生成 `assets/viora.ico`;
3. 用新图覆盖 `assets/logo.png`;
4. 重新编译。任务栏与桌面快捷方式的旧图标是 Windows 图标缓存的残留,
   重启资源管理器或首次安装到新机器时会正常显示新图标。

## 5. 「检查更新」的行为

`AboutViewModel.CheckUpdatesAsync` 目前是**诚实的无服务器实现**:提示该版本未配置更新
服务器,并打开 `AppLinks.Releases`。未来接入自动更新时,在 `AppLinks` 增加
`UpdateApiUrl` 并替换该实现即可;文案在语言包 `About.CheckUpdates.*` 键中。

## 6. 关于页 / 法律页的正文

同样在语言包:`About.Description`、`About.License`、`Legal.UserAgreement.Body`、
`Legal.PrivacyPolicy.Body` 等。第三方组件列表硬编码于 `AboutViewModel` /
`LegalViewModel`(数量少,暂未数据化;新增依赖时同步补上)。
