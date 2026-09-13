# Viora 插件开发指南

Viora 的扩展契约:插件能做的一切都通过 `Viora.PluginSdk` 表达(这是你唯一需要引用的程序集),并经 `IPluginContext` 注册。

> SDK 状态说明:`IVioraPlugin` / `VioraPluginBase` / `IPluginContext` 接口自 1.0 起保持稳定。
> 新增内置风格(马赛克、油画、简笔画、水彩)走的正是与插件相同的 `RegisterPreset` 通道,
> 无需为它们改动 SDK;现有插件无需重新编译即可继续运行。

## 1. 插件的结构

```text
MyPlugin/
├── plugin.json        # 清单(发现阶段只读元数据,不加载 DLL)
├── MyPlugin.dll       # 入口程序集(名称与清单 entryAssembly 一致)
└── *.dll              # 插件私有依赖(优先从此文件夹解析)
```

`plugin.json`:

```json
{
  "id": "com.author.pluginname",
  "displayName": "Plugin Name",
  "version": "1.0.0",
  "author": "Author",
  "description": "What it does.",
  "homepage": "https://example.org",
  "repository": "https://github.com/author/pluginname",
  "requiredHostVersion": ">=1.0.0 <2.0.0",
  "entryAssembly": "MyPlugin.dll",
  "typeName": "Author.PluginName.PluginClass",
  "capabilities": ["convert.preset", "localization.strings"],
  "dependencies": []
}
```

> **Id 约定:** 以 `builtin.` 开头的预设 Id 保留给 Viora 内置功能——插件页的"系统预设"区块
> 只列出该前缀的预设。第三方插件请使用反向域名风格(如 `com.author.pluginname`),
> 你的预设会出现在转换页的预设列表中,但不会混入"系统预设"区块。

## 2. 入口类

```csharp
public sealed class MyPlugin : VioraPluginBase   // 来自 Viora.PluginSdk
{
    public MyPlugin() : base(new PluginMetadata(
        Id: "com.author.pluginname",
        DisplayName: "Plugin Name",
        Version: new Version(1, 0, 0),
        Author: "Author",
        Description: "What it does.",
        Homepage: null, Repository: null,
        RequiredHostVersion: VersionRange.Parse(">=1.0.0"),
        Capabilities: new[] { "convert.preset", "localization.strings" },
        Dependencies: Array.Empty<PluginDependency>())) { }

    public override Task InitializeAsync(IPluginContext context, CancellationToken ct)
    {
        context.RegisterPreset(new MyPreset());
        context.RegisterStrings(new Dictionary<string, string>
        {
            ["en::Preset.My.Name"] = "My Style",
            ["zh-Hans::Preset.My.Name"] = "我的风格",
        });
        return Task.CompletedTask;
    }
}
```

## 3. 能力(Capabilities)

| 能力 | 授权内容 |
|---|---|
| `convert.preset` | `RegisterPreset(IStylePreset)` |
| `export.format` | `RegisterExporter(IImageExporter)` |
| `ui.panel` | `RegisterSettingsPage(ISettingsPageDescriptor)` |
| `localization.strings` | `RegisterStrings(...)` |

宿主静默忽略超出声明能力的注册(仅记录警告)——这是未来权限系统的预留接口。

## 4. 编写风格化预设

一个预设 = 元数据 + `BuildPipeline(parameters)`,后者返回有序的 `IImageProcessingStage` 列表。
每个阶段接收 `ImageProcessingContext`(源缓冲、工作缓冲、参数),并必须:

- 在像素循环内响应 `CancellationToken`(至少每行检查一次),
- 长时间工作时上报 `IProgress<StageProgress>`,
- **不引用 WPF / UI 框架类型**——只操作 `IImageBuffer`(BGRA 字节),
- 相同输入与参数下结果确定(测试依赖这一点)。

参考实现:`samples/SamplePlugin`(Duotone 预设,约 30 行阶段代码)。

内置风格的管线阶段源码位于 `src/Viora.Features/`,可直接借用思路:

| 预设 | 管线 |
|---|---|
| Anime Vector | 预处理 → 保边平滑 → 量化 → 区域合并 → 阴影色块 → 边缘描墨 |
| 马赛克 | 分块平色(Pixelate)→ 砖缝与釉面抖动(Grout) |
| 梵高油画 | Kuwahara 保边滤波 → 笔触流场浮雕(StrokeRelief)→ 色板分级(Posterize) |
| 简笔画 | 亮度 → Sobel 边缘 → 阈值/膨胀 → 纸面合成(InkSketch) |
| 水彩画 | 湿画平滑(Wet)→ 颜料量化(Pigment)→ 边缘沉淀(EdgePooling)→ 纸纹(PaperGrain) |

## 5. 生命周期与打包

- **安装** — 插件页 → *从文件安装…* → 选择 `.vplugin`(根目录含 `plugin.json` 的 zip)。宿主校验清单后解压到 `plugins/<id>/`。
- **启用/停用** — 插件卡片上的开关;立即生效、持久化、无需重启。
- **卸载** — 垃圾桶图标 → 确认 → 关闭、卸载、删除文件夹。
- **兼容性** — `requiredHostVersion` 与当前宿主不匹配时,插件显示为*不兼容*并附本地化说明,永远不会初始化。
- **故障遏制** — 抛异常的插件被标记为*加载失败*;Viora 继续运行;日志可在插件页查看。

用示例脚本作为打包模板:`tools/build-sample-plugin.ps1`。

## 6. 隔离模型说明

- 每个插件加载到独立的**可回收 `AssemblyLoadContext`**;`Viora.Core` 与 `Viora.PluginSdk` 与宿主的副本统一,其余程序集从你的文件夹解析。
- 停用/卸载时发生卸载;如果插件泄漏了存活类型(静态事件、运行中的线程),宿主记录*延迟卸载*并保持其"已停用但驻留内存"直到重启。干净的 `ShutdownAsync` 可避免这一点。
- 插件**不会**获得原始文件系统/注册表访问——只有上文的作用域注册面。沙箱化存储将在未来以新能力形式提供。
