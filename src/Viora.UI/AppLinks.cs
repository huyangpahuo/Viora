namespace Viora.UI;

/// <summary>
/// 应用对外链接的唯一配置处。「关于 / 帮助 / 反馈 / 社区 / 赞助 / 法律信息」
/// 各页面均从此处取值 —— 换官网域名、换赞助渠道时只改这一个文件。
/// </summary>
public static class AppLinks
{
    /// <summary>项目仓库主页(作者:huyangpahuo)。</summary>
    public const string Repository = "https://github.com/huyangpahuo/Viora";

    /// <summary>官方网站。当前以仓库主页代替;拥有独立域名后改这里即可。</summary>
    public const string OfficialSite = Repository;

    /// <summary>版本发布页(「检查更新」引导用户前往这里)。</summary>
    public const string Releases = $"{Repository}/releases";

    /// <summary>赞助页。开通 GitHub Sponsors 后替换为 https://github.com/sponsors/&lt;用户名&gt; 。</summary>
    public const string Sponsors = "https://github.com/sponsors/huyangpahuo";

    /// <summary>缺陷反馈(GitHub Issue 模板)。</summary>
    public const string BugReport = $"{Repository}/issues/new?template=bug_report.md";

    /// <summary>功能建议(GitHub Issue 模板)。</summary>
    public const string FeatureRequest = $"{Repository}/issues/new?template=feature_request.md";

    /// <summary>社区讨论区。</summary>
    public const string Discussions = $"{Repository}/discussions";

    /// <summary>在线文档 Wiki。</summary>
    public const string Wiki = $"{Repository}/wiki";

    /// <summary>常见问题页。</summary>
    public const string WikiFaq = $"{Wiki}/FAQ";
}
