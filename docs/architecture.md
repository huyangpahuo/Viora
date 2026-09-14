# Viora 架构

> 配套文档:`00-inspection-and-plan.md`。本文描述系统**实际构建**的样子。

## 1. 分层总览

```text
┌────────────────────────────────────────────────────────────┐
│ Viora.App (net8.0-windows, exe)                            │
│  组合根:DI 装配、App.xaml、MainWindow、全局异常遏制、       │
│  导入/导出桥、插件宿主启动序列                              │
├────────────────────────────────────────────────────────────┤
│ Viora.UI (net8.0-windows)                                  │
│  Shell(侧边栏导航 + 页面缓存)、Pages(+VM)、               │
│  设计系统(Themes/*.xaml 令牌与控件样式)、                  │
│  {loc:Translate} 实时本地化绑定、ThemeManager、             │
│  PresetCatalog、IFeatureRegistry、导入/导出代理             │
├────────────────────────────────────────────────────────────┤
│ Viora.Features / Convert.Common / Convert.AnimeVector /    │
│ Convert.Styles                                             │
│  内置功能 = IBuiltInFeature,经与插件相同的 IPluginContext   │
│  通道注册。AnimeVector:预处理 → 平滑 → 量化 → 合并 →       │
│  阴影色块 → 描墨(全托管、并行、可取消)。                   │
│  Convert.Styles:35 个内置风格预设(马赛克/油画/简笔画/水彩  │
│  + 30 个扩展风格),共享原语库 Core/ImageOps。              │
├────────────────────────────────────────────────────────────┤
│ Viora.Core (net8.0 — 无 WPF 引用)                          │
│  IImageProcessingStage / IImageConversionEngine /          │
│  IStylePreset / ISettingsService / ILocalizationService /  │
│  IPluginHost / IPluginContext / IVioraPlugin 契约面,       │
│  IImageBuffer(BGRA)、VersionRange                         │
├────────────────────────────────────────────────────────────┤
│ Viora.PluginSdk (net8.0)                                   │
│  IVioraPlugin + VioraPluginBase — 第三方插件唯一需要        │
│  引用的程序集                                              │
├────────────────────────────────────────────────────────────┤
│ Viora.Infrastructure (net8.0-windows)                      │
│  JsonSettingsService(原子写)、ImageConversionEngine       │
│  (阶段执行器 + 进度映射)、AssemblyPluginHost + 可回收       │
│  PluginLoadContext、PluginManifest、WIC 导出器              │
│  (PNG/JPEG/BMP)、ImportService(识别 EXIF)、AppPaths、     │
│  FileLoggerProvider                                        │
├────────────────────────────────────────────────────────────┤
│ Viora.Localization (net8.0)                                │
│  JsonLocalizationService — 内嵌 en/zh-Hans 语言包,         │
│  英文回退,插件字符串注册("lang::Key")                     │
└────────────────────────────────────────────────────────────┘
```

**强制边界:** `Viora.Core`、`Viora.PluginSdk`、`Viora.Features`(预设/阶段代码)与
`Viora.Localization` **零** `PresentationFramework`/`PresentationCore` 引用。
WPF 类型只出现在 `Viora.UI`、`Viora.App` 以及 `Viora.Infrastructure` 的适配面
(解码器、导出器)——即任务书指定的"编解码适配层"。

## 2. 关键决策(第 11 节协议)

| 决策 | 备选项 | 选定 | 理由 / 权衡 |
|---|---|---|---|
| Anime Vector 算法 | ONNX 分割辅助;混合方案 | **经典 CV 管线** | 直接落地风格分析规则(调色板坍缩 + 硬边阴影 + 形状简化);零模型/授权负担;完全离线。ONNX 升级路径 = 一个插件阶段,无需重构。 |
| MVVM 工具包 | 手写 ObservableObject | **CommunityToolkit.Mvvm 8.3** | 源生成消除样板;MIT;生态默认。 |
| 插件隔离 | 进程外;AppDomain(.NET 8 已移除) | **可回收 AssemblyLoadContext** | 进程内卸载 + 依赖隔离。权衡:泄漏存活类型的插件保持驻留但停用(记录为"延迟卸载")。 |
| 本地化格式 | RESX | **JSON 语言包** | 加文件即加语言、无需重编译、便于热切换;键覆盖率由测试保证而非编译器。 |
| 设置持久化 | 注册表;分节文件 | **单版本化 JSON**(`%LOCALAPPDATA%\Viora\settings.json`) | 临时文件+替换原子写;损坏文件备份为 `.corrupt` 并重置默认;以 `SchemaVersion` 迁移。 |
| 解决方案布局 | 合并为单项目 | **按任务书 §4 的 8 项目** | 每个项目对应一条强制边界;未添加任务书树之外的东西。 |

## 3. 图像管线契约

- `IImageConversionEngine.ExecuteAsync(pipeline, source, parameters, previewQuality, progress, ct)`
  在线程池上顺序执行各阶段;每个阶段内部并行像素循环并上报 `StageProgress`,
  引擎将其映射为整体 `PipelineProgress`。
- 取消:引擎在阶段之间检查令牌;阶段在循环内检查
  (量化/合并/双色调会及时抛出 `OperationCanceledException`——有测试覆盖)。
- 预览与导出:调整参数时 UI 以降采样缓冲运行管线(`PreviewMaxDimension`),
  导出时以完整分辨率重跑(`ExportMaxDimension`)。
- 转换页的自动转换策略为"最新优先":预设切换/参数调整会取消上一次预览并重启,
  仅最新一轮运行会清除忙碌标记(见 `ConvertViewModel.AutoConvertAsync`)。
- `IImageBuffer` 是朴素的 BGRA 字节网格——无框架类型——因此各阶段可无头单元测试
  (见 `AnimeVectorStageTests`、`BuiltInStylePipelineTests`、`StyleRenderDump`)。

## 4. 插件系统(实际实现)

1. **发现** — 等价于 `%LOCALAPPDATA%\Viora.plugins` 的 `plugins/<id>/plugin.json`
   仅元数据扫描;畸形目录记录日志并跳过。
2. **版本检查** — 加载**之前**比对 `RequiredHostVersion` 区间与入口程序集版本;
   不兼容的插件在插件页显示为「不兼容」并附本地化说明。
3. **加载** — 每个插件一个可回收 `PluginLoadContext`;`Viora.Core`/`Viora.PluginSdk`
   与宿主统一;插件私有 DLL 优先从插件文件夹解析。
4. **初始化** — 经能力校验的 `IPluginContext`(`convert.preset`、`export.format`、
   `ui.panel`、`localization.strings`);宿主边界的全部调用都有包装:抛异常的插件
   变为 `Failed` 状态 + 日志,绝不崩溃。
5. **启用/停用** — 持久化于 `Plugins.DisabledPlugins`;停用时尽可能卸载。
6. **安装/卸载** — `.vplugin`(根目录含 `plugin.json` 的 zip);卸载 = 关闭 +
   卸载 + 删除文件夹(IO 错误记日志,下次启动重试)。
7. **示例生命周期** — `samples/SamplePlugin`(Duotone 预设)+ `tools/build-sample-plugin.ps1`
   产出 `DuotoneSample.vplugin`;安装/启用/停用/卸载全流程可在界面内完成。

## 5. 待回顾项(有意保留、已记录的捷径)

- **SVG 导出**:管线已计算区域边界,但先交付栅格输出;SVG 写出器是下一个增量
  (边界已在 `ConsolidateStage` 提取)。
- **缓存设置**:开关/大小已持久化并接入路径提供者;预览结果缓存本体随 SVG/性能
  阶段一起交付。
- **插件市场**:`InstallFromPackageAsync` 接受本地路径;未来的 `InstallFromUriAsync`
  可直接嵌入 `IPluginHost`,不动接口。
- **界面缩放设置**:已持久化但需重启后按窗口生效(WPF 布局缩放无法简单热切换);
  设置页已如实说明("立即生效"字样只出现在真正立即生效的设置旁)。
