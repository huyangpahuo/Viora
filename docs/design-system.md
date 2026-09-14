# Viora 设计系统

Viora 设计系统以 WPF `ResourceDictionary` 令牌(token)形式编码于 `src/Viora.UI/Themes/`,
派生自 `00-inspection-and-plan.md` §3 的 NavigationBar 设计语言提取。任何页面 XAML 都不
硬编码颜色、圆角或字号——所有视觉数值都是命名令牌。

## 1. 令牌来源(来自设计语言提取)

| 提取观察 | Viora 令牌转化 |
|---|---|
| 深色锚定窗口 `#222222` + 浅色悬浮条 `#DDDDDD` | 深色外壳 `Color.Background #1C1C24` + 浮起表面;浅色主题以 `Tokens.Light.xaml` 提供 |
| 圆角条(`CornerRadius=10`)、圆形(r=40/34) | `Radius.Large=10`(卡片/表面)、`Radius.Medium=8`(控件)、`Radius.Small=4` |
| 基于透明度的文字层级(`#44333333` → `#333333`) | `Color.Text.Primary / .Secondary / .Disabled` 色阶,不再用 alpha 叠加 |
| `CubicEaseInOut` 500ms 动效 | `Motion.Quick 120ms`(悬停)、`Motion.Standard 200ms`(选中)、`Motion.Slow 350ms`(面板),共用 `CubicEase` |
| 移动圆点指示器 | 侧边栏的选中胶囊(动画背景切换) |
| 宽松的 20 单位节奏 | 间距刻度 4/8/12/16/24/32 |
| 趣味极简、扁平、无阴影 | 扁平控件;仅保留两个克制的层级令牌(`Shadow.Card`、`Shadow.Flyout`)用于弹层 |

## 2. 令牌文件

- `Themes/Tokens.xaml` — 颜色角色、字体样式、间距、圆角、层级、动效(深色默认)
- `Themes/Tokens.Light.xaml` — 浅色主题颜色角色(由 `ThemeManager` 运行时切换)
- 另有 `Tokens.Ocean / Forest / Plum / Sunset / Midnight / Sand / Sakura / Mint.xaml` 等配色变体
- `Themes/Controls.xaml` — `VioraButton`、`SecondaryButton`、`DangerButton`、`IconButton`、
  `SaveIconButton`、`ModePill`、`VioraToggle`、`VioraSlider`、`VioraCombo`、`VioraTextBox`、
  `VioraCard`、`LinkButton`、`FormCheckBox`
- `Themes/Navigation.xaml` — 侧边栏样式(`NavGroupHeader`、`NavListBox`、`NavListBoxItem`)
- `Themes/PageChrome.xaml` — 共享的页面标题/滚动模板(`PageChrome` 控件)

## 3. 颜色角色

| 令牌 | 深色 | 浅色 | 用途 |
|---|---|---|---|
| `Color.Background` | `#1C1C24` | `#F2F2F5` | 窗口基底 |
| `Color.Surface` | `#26262F` | `#FFFFFF` | 卡片、侧边栏 |
| `Color.SurfaceElevated` | `#2F2F3A` | `#EDEDF2` | 输入框、悬停填充、弹层 |
| `Color.Accent` | `#5AC8FA` | `#2E9FD8` | 主操作、选中态 |
| `Color.Text.Primary/Secondary/Disabled` | `#F2F2F5` / `#A8A8B4` / `#5C5C68` | 反转色阶 | 文字层级 |
| `Color.Danger / Success / Warning` | 语义色 | 语义色 | 错误、确认、状态 |

## 4. 信息架构(§7.3 设计依据)

侧边栏分组按产品面排序:

1. **创作** — 首页、转换。产品的主循环。
2. **社区** — 社区。独立的交流与(未来的)插件分享空间。
3. **系统** — 插件、设置。产品基础设施。
4. **信息** — 关于、赞助、法律信息。次级聚合,视觉上独立成组,不与功能导航竞争;
   新功能进入「创作」,聚合保持稳定(帮助与反馈内容已并入关于页)。

分组标题本地化;条目是数据(`NavigationItem`),不是每页硬编码的 XAML,
未来插件加入的面板可以复用同一套信息架构。

## 5. 规则

1. 不出现原生 WPF 外观:每个交付的控件都有 Viora 样式。
2. 不写魔法数:颜色/圆角/尺寸只来自令牌(主题切换用 `DynamicResource`)。
3. 层级优先通过字重/字号/颜色表达,边框和色块其次。
4. 每个交互元素在样式中定义悬停/按下/禁用态——一处定义,处处复用。
5. 动效缓动一致(`CubicEaseInOut` 家族),控件短、面板长。
