# Release Notes / 发布说明

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
