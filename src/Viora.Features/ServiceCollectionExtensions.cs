using Microsoft.Extensions.DependencyInjection;

namespace Viora.Features;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 全部风格预设已插件化(src/Viora.Plugins/*,由官方仓库分发)。
    /// 保留空实现以维持宿主装配结构;导出器由宿主(App)直接注册。
    /// </summary>
    public static IServiceCollection AddVioraFeatures(this IServiceCollection services) => services;
}
