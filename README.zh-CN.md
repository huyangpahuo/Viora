# Viora

**创作 · 转换 · 风格化**

[English](README.md)

Viora 是一个模块化的 Windows 桌面图像创作、转换与风格化工作室,基于 WPF/.NET 8 构建。内置五种风格转换预设——**Anime Vector**(扁平几何二次元 / 矢量插画)、**马赛克**、**梵高油画**、**简笔画**、**水彩画**;其插件体系保证未来的所有能力都可以在不重构应用的前提下加入。

```text
                         Viora
                           │
              ┌────────────┴────────────┐
              │                         │
        Viora Core                 Feature System
              │                         │
    ┌─────────┼─────────┐       ┌───────┼────────┐
    │         │         │       │       │        │
   UI      设置       i18n   Convert   Edit   Plugins
                              │
                              ├── Anime Vector(内置)
                              ├── 马赛克 / 油画 / 简笔画 / 水彩(内置)
                              ├── Duotone(示例插件)
                              └── 你的插件
```

## 功能特性

- **五种内置风格预设** — Anime Vector(调色板坍缩为少量硬边扁平色块、块状阴影、区域合并与边界简化、可选深色描边)、马赛克(分块平色 + 砖缝)、梵高油画(Kuwahara 笔触 + 厚涂肌理)、简笔画(墨线勾勒 + 铅笔调子)、水彩画(湿画罩层 + 边缘沉淀 + 纸纹)。全部算法源自经典计算机视觉管线,托管代码、并行执行。
- **完整转换工作流** — 拖放或选择器导入、实时预览、原图/结果分屏与并排对比(分割手柄与图像内容精确对齐)、参数滑杆与重置、重新运行、进度显示与取消、PNG/JPEG/BMP 导出。
- **插件系统** — 基于清单的发现、版本兼容检查、隔离加载(可回收 `AssemblyLoadContext`)、界面内安装/启用/停用/卸载、能力注册门控、错误遏制:坏插件绝不拖垮宿主。插件页同时展示全部"系统预设"。
- **双语界面** — 完整的英文与简体中文语言包;语言切换即时生效、无需重启。插件可通过 `lang::Key` 条目自带字符串。
- **真实设置** — 九个持久化分类(常规、外观、语言、图像处理、导出、插件、性能、缓存、隐私、开发者),全部有真实效果:主题与日志级别即时生效,分辨率上限与质量直接作用于管线。
- **完整产品基础设施** — 关于(诚实的更新检查)、帮助、反馈(缺陷/功能/一般,路由到 GitHub Issues)、数据驱动的社区列表、赞助、用户协议/隐私/开源许可页面。
- **本地离线** — 全部处理在本机完成;无账号、无上传、默认无遥测;日志路径默认脱敏。

## 截图

> 界面截图(首页 / Convert 使用 Anime Vector 处理参考图 / 插件 / 设置)将放入 `assets/screenshots/` —— 界面为深色、侧边栏分组、强调色高亮风格。

## 架构(摘要)

八个项目强制层边界(详见 [`docs/architecture.md`](docs/architecture.md)):

| 项目 | 职责 |
|---|---|
| `Viora.App` | 组合根:DI、全局异常遏制、桥接 |
| `Viora.UI` | Shell、页面 + MVVM、设计系统、本地化绑定 |
| `Viora.Core` | 仅契约 —— **无 WPF 引用** |
| `Viora.Features` | 内置 Anime Vector 预设(与插件同路注册) |
| `Viora.PluginSdk` | 第三方插件唯一需要引用的程序集 |
| `Viora.Infrastructure` | 引擎、设置 JSON、插件宿主、编解码、日志 |
| `Viora.Localization` | JSON 语言包(en、zh-Hans) |
| `tests/*` | Core 管线、Feature 阶段、Infrastructure 往返测试 |

## 图像处理方案

Anime Vector 预设采用**经典计算机视觉管线**——在对比了经典方案 / 分割辅助方案 / 混合方案与参考图的实测特征(六大扁平色块、硬边阴影、无渐变、对比度分区)后做出选择,分析见 `docs/00-inspection-and-plan.md`:

```text
预处理(饱和度)→ 保边平滑 → k-means 量化(有界调色板)
  → 区域合并(消噪点) → 阴影色块(硬边双色调拆分)
  → 边缘描墨(可选深色边界)
```

每个阶段都是托管代码,并行执行、可取消,并通过风格规则属性测试(调色板上限、区域一致性)验证,而非脆弱的黄金像素比对。

## 插件系统

在插件页面安装 `.vplugin` 包、切换开关、卸载——全程无需重启。能力(`convert.preset`、`export.format`、`ui.panel`、`localization.strings`)决定插件可注册的内容。`samples/SamplePlugin` 附带可运行的示例插件与一键打包脚本。完整指南:[`docs/plugin-development.md`](docs/plugin-development.md)。

## 安装

**从源码运行(当前推荐):**

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. `dotnet build Viora.sln -c Release`
3. `dotnet run --project src/Viora.App -c Release`

**便携模式:** 在 `Viora.exe` 旁创建空的 `portable.marker` 文件,设置、日志、缓存与插件将保存在可执行文件旁,而非 `%LOCALAPPDATA%\Viora`。

## 使用方法

1. **转换** 页面 → 拖入图片(支持 PNG/JPEG/BMP/GIF/TIFF/WebP)。
2. 选择风格预设:**Anime Vector** / **马赛克** / **梵高油画** / **简笔画** / **水彩画**;每个预设带自己的参数滑杆,拖动时预览自动以预览分辨率重新运行。
3. 拖动分割手柄对比原图/结果(点击任意位置可直接跳转分割线;或并排显示)。
4. **导出** 时以完整分辨率重新运行并写入 PNG/JPEG/BMP。

设置 → 语言可即时切换界面;外观可切换深色/浅色;开发者选项解锁完整错误细节与日志详细度。

## 开发

```bash
dotnet build Viora.sln            # 构建全部
dotnet test Viora.sln             # 运行 31 个测试
dotnet run --project src/Viora.App
```

### 构建示例插件

```powershell
powershell -File tools/build-sample-plugin.ps1
# → samples/SamplePlugin/DuotoneSample.vplugin(在插件页面安装)
```

### 插件开发

见 [`docs/plugin-development.md`](docs/plugin-development.md) —— 清单、能力、预设阶段、隔离模型、打包。

## 本地化

语言包为内嵌 JSON(`src/Viora.Localization/Assets/*.json`),带英文回退。贡献新语言 = 添加翻译 JSON 文件并注册显示名;无需任何 UI 改动。所有用户可见字符串均通过 `{loc:Translate Key}` 绑定,切换即时生效。

## 路线图

- [ ] 基于已简化区域边界的 SVG/矢量导出(边界已计算)
- [ ] 带 LRU 淘汰的预览结果缓存
- [ ] 应用内文档查看器
- [ ] 插件市场(`InstallFromUriAsync`)与沙箱化插件存储
- [x] 更多内置预设 —— 马赛克 / 梵高油画 / 简笔画 / 水彩画已随本版本交付

## 常见问题

**我的图片会被上传吗?** 不会。全部处理都在本地;隐私页面记录了确切行为,日志路径默认脱敏。

**为什么结果看起来扁平/海报化?** 这正是该风格的本质——参考图就是硬边阴影的扁平几何矢量艺术。调色板数量与细节保留参数控制简化的程度。

**大图转换比较慢。** 调整时使用预览质量;导出才以完整分辨率运行。设置 → 图像处理中的上限可控制开销。

**可以添加自己的风格吗?** 可以——这正是 Viora 的设计目的。参见插件指南:一个预设 = 一份清单 + 一个管线构建器 + 若干阶段类。

## 参与贡献

欢迎 Issue 和 Pull Request。请保持 PR 与架构边界一致(`Viora.Infrastructure` 以下不出现 WPF 类型)、为管线行为附带测试,并保证两种语言包对新字符串完整覆盖。

## 许可证

Viora 基于 **GNU AGPL-3.0** 许可证发布(见 `LICENSE`)。第三方组件及其许可证在应用内(关于 → 第三方组件)与 `docs/design-system.md` 中列出。

## 赞助

Viora 免费开源。赞助信息见应用内 **赞助** 页面与 GitHub Sponsors——完全自愿,无任何付费门槛。

## 社区

社区页面由数据驱动;真实的交流空间(QQ/Discord/Telegram/论坛)上线后会列在那里。
