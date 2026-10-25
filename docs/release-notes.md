﻿# Release Notes / 发布说明

## 1.0.2

### 中文

**新增**
- 客户端接入官网(viora-web)的社区插件目录:作者在网站发布、记录在数据库的插件(安装包托管在作者自己的 GitHub 仓库)现在出现在插件市场,可直接搜索安装;
- 界面新增**日语语言包**(日本語):设置里即可切换,600 余条文案全部翻译;
- 官方风格目录从 55 款扩到 **95 款**,新增 40 个风格插件:艺术运动(浮世绘、新艺术、装饰艺术、包豪斯、瑞士国际主义、构成主义、风格派、立体派)、设计与网络亚文化(未来主义、超现实主义、达达、至上主义、孟菲斯、粗野主义、迷幻、Vaporwave、Synthwave、Y2K、Frutiger Aero、GeoCities)、历史文化(泥金手抄本、拜占庭圣像、埃及象形、波斯细密画、莫卧儿细密画、凯尔特结、皮影戏)、数学与科学(莫尔纹、Voronoi、反应扩散、元胞自动机、L-System、Metaball、流场、纹影成像)、光学与摄影(鱼眼、太阳化、长曝光、红蓝立体、光栅立体),全部保留照片内容、不做过度抽象;
- 插件市场支持把 zip 直接拖进窗口安装,和"从市场安装"等效;
- 第三方插件可以自带多语言文案,随插件一起分发。

**移除**
- 应用内「插件工坊」整页移除(含源码编辑、预览、打包、registry 整合等功能);插件制作将改为 **VS Code 扩展**形态重新提供,教程见 docs/06,相关设置项预留未删。

**修复**
- 界面语言下拉对第三方语言包误显示为原始代码或重复的「English」:现在统一读取语言包自带的名称(如「日本語」),选中态随之正确;
- 侧边栏在日语等长文案下溢出出现横向滚动条:标题与副标题改为自动换行;
- 界面字体下拉在未自定义时显示为空:现在显示当前实际生效的默认字体名。
- 网络不佳时插件市场一直显示旧目录:目录刷新超时从 8 秒放宽到 20 秒,拉取成功的目录缓存到磁盘(之后离线也显示最新),编译期内置快照同步更新到 95 款;
- 平滑图片使用「弦丝画」风格时画面毫无变化的 bug;
- 8 个艺术运动风格升级到 1.1.0:重写了算法,新版保留照片原本的内容与结构,不再是"几何色块抽象画"。

### English

**New**
- The client now consumes the community plugin catalog from the Viora website: plugins published by authors on the site (recorded in its database, packages hosted in the authors' own GitHub repos) appear in the plugin market for direct install;
- A **Japanese interface language pack** (日本語): switchable in Settings, with all 600+ strings fully localized;
- The official style catalog grows from 55 to **95 styles** with 40 new plugins: art movements (Ukiyo-e, Art Nouveau, Art Deco, Bauhaus, Swiss Style, Constructivism, De Stijl, Cubism), design & web subcultures (Futurism, Surrealism, Dada, Suprematism, Memphis, Brutalism, Psychedelic, Vaporwave, Synthwave, Y2K, Frutiger Aero, GeoCities), historical & cultural (Illuminated Manuscript, Byzantine Icon, Egyptian Hieroglyph, Persian Miniature, Mughal Miniature, Celtic Knotwork, Shadow Puppet), math & science (Moiré, Voronoi, Reaction Diffusion, Cellular Automaton, L-System, Metaball, Flow Field, Schlieren), optics & photography (Fisheye, Solarization, Long Exposure, Anaglyph 3D, Lenticular) — all preserve the photo's content instead of turning it into abstraction;
- The plugin market now accepts dragging a plugin zip onto the window to install, same as installing from the market;
- Third-party plugins can ship their own localized strings alongside the plugin.

**Removed**
- The in-app plugin **Workshop** page is removed (source editing, preview, packaging, registry integration); plugin authoring will return as a **VS Code extension** — see docs/06 for the tutorial. Reserved settings keys are kept.

**Fixed**
- The language dropdown showed the raw code or a duplicated "English" for third-party packs: all dropdowns now use the pack's own display name (e.g. 日本語) and the check mark follows;
- The sidebar overflowed with a horizontal scrollbar for long languages like Japanese: titles and subtitles now wrap automatically;
- The interface-font dropdown was blank when no custom font was set: it now shows the currently effective default font name.
- The market kept showing a stale catalog on poor networks: the catalog refresh timeout was raised from 8s to 20s, a successfully fetched catalog is now cached to disk (so even offline sessions show the latest), and the built-in snapshot shipped with the app was refreshed to the 95-style catalog;
- String Art produced no visible change on smooth photos;
- The 8 art-movement styles are rebuilt (v1.1.0) to preserve the photo's original content and structure instead of producing abstract color blocks.

---

## 1.0.1

### 中文

**Viora 1.0.1** —— 插件市场支持在线更新,导出进入全质量时代,稳定性与安全性大幅加固。

#### 新功能

- **插件在线更新**:已安装的插件发布新版本后,市场卡片上的「安装」按钮自动变为「更新」,点击即按版本覆盖升级,无需卸载重装;
- **导出全质量渲染**:预览按「预览最大边长」快速渲染;导出时自动按「导出最大边长」重新导入原图并全质量重跑——所见即所得,导出不再受预览分辨率限制;
- **下载双通道**:插件与目录下载依次尝试 GitHub raw 与 jsDelivr CDN,任一可达即可安装;
- **全部语言包外置**:简体中文、English 与第三方语言包统一放在程序旁 `languages` 文件夹,替换文件即改文案,机制完全一致;
- **默认配色「落日」**:首次启动即呈现暖色主题(已有用户的设置不受影响)。

#### 改进

- 插件安装安全加固:插件 id 白名单校验与入口程序集存在性校验,拒绝畸形安装包;
- 插件契约程序集强制使用宿主副本,插件无法再覆盖核心接口;
- 单个损坏的插件或设置文件不再影响应用启动(自动备份并按默认值继续);
- 卸载插件同时清理禁用记录,重装后直接可用;
- 插件上下文开放全部六种导出格式(PNG/JPEG/BMP/TIFF/GIF/WebP)。

#### 修复

- 手机照片 EXIF 方向识别失效导致的横竖颠倒;
- 非英文区域设置(逗号小数)下「重新生成」参数静默丢失;
- works.json 写入中断导致的作品库损坏(改为原子写入,损坏自动留档);
- 快捷键「清除」后仍按默认组合生效的问题(清除现在表示彻底禁用);
- 半成品语言包(空值文案)覆盖内置文案导致界面空白;
- 界面语言默认值与文档不符等若干小问题。

#### 升级说明

- 用 1.0.1 覆盖旧版本即可,已安装插件完全兼容,无需重装;
- 1.0.1 起语言包全部位于程序旁 `languages\` 文件夹——覆盖升级时请保留该文件夹;
- 用户数据(作品/设置/日志)位置不变。

---

### English

**Viora 1.0.1** — the plugin market learns in-place updates, exports enter the full-resolution era, and the app gets a solid round of hardening.

#### New

- **Plugin updates**: when an installed plugin ships a newer version, the market's Install button becomes *Update* and upgrades in place by version — no uninstall/reinstall needed;
- **Full-quality export**: previews render at the preview cap; exporting automatically re-imports the original and re-runs the pipeline at the export cap — exports are no longer limited by preview resolution;
- **Dual download channels**: packages and the catalog try GitHub raw first, then a jsDelivr CDN mirror;
- **All language packs externalized**: Simplified Chinese, English and third-party packs now live together in the `languages` folder with one identical mechanism;
- **New default theme**: *Sunset* greets first launches with warm tones (existing preferences are untouched).

#### Improvements

- Hardened plugin installation: plugin-id whitelist and entry-assembly validation reject malformed packages;
- Plugin contract assemblies always resolve to the host copy — plugins can no longer shadow core interfaces;
- A corrupted plugin or settings file can no longer prevent startup (auto-backed up, defaults continue);
- Uninstalling now clears disabled-flags, so reinstalling a plugin just works;
- The plugin context now exposes all six export formats (PNG/JPEG/BMP/TIFF/GIF/WebP).

#### Fixes

- Phone photos imported with wrong orientation (EXIF orientation was never applied);
- Regenerate silently losing parameter values under comma-decimal locales;
- Works library corruption when the index write was interrupted (now atomic with crash-safe backup);
- Hotkey "clear" falling back to the default gesture instead of disabling;
- Half-translated language packs (empty values) blanking out built-in strings;
- Various smaller issues.

#### Upgrade notes

- Overwrite your 1.0.0 install with 1.0.1 — installed plugins stay compatible, no reinstall needed;
- Language packs now live in the `languages` folder next to the exe — keep that folder when overwriting;
- User data locations (works / settings / logs) are unchanged.
