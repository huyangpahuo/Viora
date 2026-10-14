# AGENT.md — 给 AI 代理的工作约定

Viora 是**本地优先**的 Windows 图片风格化应用(WPF / .NET 8),面向普通用户,不是开发者工具。
本文件是最高优先级的工作约定:代理在本仓库工作前必须先读这一页,再读 [docs/](docs/)。

## 1. 项目地图

| 路径 | 内容 |
|---|---|
| `src/Viora.App` | 主程序组合根:DI 装配、异常遏制、文件日志、UI↔Infrastructure 桥接、`languages/` 语言包 |
| `src/Viora.UI` | 全部界面(四大页 + Shell + 主题令牌 + 控件);不引用 Infrastructure |
| `src/Viora.Core` | 纯契约(管线/插件/设置/作品),**禁止出现 WPF 类型** |
| `src/Viora.Infrastructure` | 实现:WIC 图像引擎、插件宿主(ALC 隔离)、JSON 设置/作品库、导出器 |
| `src/Viora.Localization` | 语言包加载(exe 旁 `languages\*.json`,内置与第三方同一机制) |
| `src/Viora.PluginSdk` | 第三方插件**唯一**允许引用的程序集 |
| `plugin-sources/` | 95 个官方插件的算法源码(gitignore,本地);每插件完全自足,命名空间 `Viora.Plugin.<名>` |
| `Viora-plugins/` | 官方插件分发仓(gitignore,本地):`packages/` + `registry.json` |
| `icon库/classic/` | 图标 SVG 源(Font Awesome Free),`Icons.xaml` 由它生成 |
| `tools/` | 本地验证器(gitignore):`InstallVerifier` 插件安装链路回归 |
| `vscode-workshop/` | 插件工坊的 VS Code 扩展(重新设计中,Node 工具链):脚手架/校验/打包/装进 Viora,教程与任务书见 docs/06 |
| `docs/` | 文档集 01–05,改代码必须同步改文档 |

**硬边界**:`Viora.Core` 不引用 WPF;`Viora.UI` 不引用 Infrastructure(由 App 组合根桥接);`src/` 里没有任何风格算法代码——算法只以插件形态存在,官方与第三方走同一条加载链路。

## 2. 必须遵守的约定

**发布打包(docs/04 §1.1)**:所有对外发布的可执行体一律**自包含 + 单文件**:

```bash
dotnet publish src/Viora.App -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none ^
  -o artifacts/<名>-<版本>-win-x64
```

三个 `-p:` 参数缺一不可。`languages\`、`plugins\` 等数据目录永远外置,单文件只合并程序集。目录形态仅用于本地调试。

**发布信息**:`release-notes.md`(仓库根目录,已被 .gitignore 排除)记录"新增了什么/删除了什么/变更了什么"。**只在用户明确要求发布时创建或更新**,平时禁止写入。

**图标**:应用内一切矢量图标**只**从 `icon库/classic/` 的 Font Awesome SVG 选取并同步进 `src/Viora.UI/Assets/Icons.xaml`(键名 `Icon.<名称>`);禁止引入其它图标库、内联随机 SVG 或临摹变形。

**语言包**:`src/Viora.App/languages/` 下 `en.json` 与 `zh-Hans.json` 键集必须同步,新增界面文案两包同时加;`_template.json` 是第三方模板。注意 `Preset.*`、`Param.*`、`Theme.Color.*` 是**运行时动态拼接的键**(插件 DisplayNameKey、主题编辑器),不要因为"静态搜不到引用"而删除;同样被已移除的工坊(将改为 VS Code 扩展)预留的 `Workshop.*`、`Settings.Workshop.*`、`Appearance.Workshop*` 设置字段也不要删。

**注释**:只写"为什么"(约束、坑、决策),不复述下一行代码;不留调试注释、注释掉的死代码。改注释时连同它解释的代码一起审视。

**死代码**:公开成员若在全仓(含 XAML 绑定与动态键拼接)无消费者,应当场删除,不留"以后可能用";**例外**:`Viora.Core/Pipeline` 契约与 `Viora.PluginSdk` 是插件生态的 API 面,消费者在 gitignore 的 `plugin-sources/`,删除前先确认。设置模型里的每项都必须有 UI 或读取方,禁止新增"幽灵设置项";删除设置字段的兼容性是安全的(旧 settings.json 里的多余键会被忽略)。

**数据与隐私**:无账号、无上传、无遥测;日志路径脱敏;便携模式 = exe 旁 `portable.marker`,否则数据在 `%LOCALAPPDATA%\Viora`。

## 3. 常用命令

```bash
dotnet build Viora.sln -c Release          # 全量构建
dotnet run --project src/Viora.App -c Release
dotnet publish …                            # 见 docs/04 §1.1
python -c "import json;[json.load(open(f, encoding='utf-8-sig')) for f in ['src/Viora.App/languages/en.json','src/Viora.App/languages/zh-Hans.json']]"
```

改动插件安装/加载链路后,本地跑 `tools/InstallVerifier` 回归(gitignored,仅本地存在)。

## 4. 文档同步

改功能必须同步对应文档:功能 → `docs/01`、架构/文件职责 → `docs/02`、插件契约 → `docs/03`、发布形态 → `docs/04`、设置/语言包 → `docs/05`;面向用户的概述同步 `README.md` 与 `README.zh-CN.md` 双语。文档只陈述当前事实,不写"未来计划"(规划中的东西一句话带过即可)。
