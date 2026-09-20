# Viora

**Give every picture a new style.**

English · [简体中文](README.zh-CN.md)

Viora is a **local-first** photo stylization app for Windows (WPF / .NET 8). Import a picture and apply art-style plugins in one click — the official repository currently ships **55 styles** (watercolor, oil painting, low-poly, chalkboard, comic, blueprint…), all installed on demand from the plugin market. Client and plugins are fully decoupled: you decide what gets installed, and uninstalling really deletes.

## Screenshots

| Stylize | My Works |
|---|---|
| ![Stylize](docs/images/风格化.png) | ![My Works](docs/images/我的作品.png) |
| **Plugin Market** | **Settings** |
| ![Plugin Market](docs/images/插件市场.png) | ![Settings](docs/images/设置.png) |

## Features

- **Stylize** — 55 art styles (all via the plugin market), live preview while tweaking sliders, batch queue, split before/after compare, history, retry on failure; everything runs locally.
- **Plugin market** — browse / search (name · author · tags · description) / category filter / one-click install & uninstall. Installing downloads a zip from the official GitHub repository into the `plugins` folder next to the app; uninstalling deletes it — adding or removing folders in Explorer works exactly the same.
- **My Works** — grid & list views, favorites, inline rename, regenerate, duplicate, export (6 formats: PNG / JPEG / BMP / TIFF / GIF / WebP).
- **Settings** — general (startup, UI language, import size cap, custom log folder), appearance (10 color schemes + custom theme editor + UI scale 80%–120%), performance (preview quality, size caps, export format & JPEG quality), plugin management (master switch + per-plugin toggle), keyboard shortcuts (visual recording; defaults: Ctrl+Enter run, Ctrl+O import, Ctrl+1~4 switch pages), check for updates (compares against GitHub Releases).
- **Bilingual + extensible language packs** — Simplified Chinese / English built in; drop a translated JSON into the `languages` folder and restart to add a new language (see [Settings & Localization](docs/05-设置与本地化.md)); PRs into the main repo are welcome too.
- **Privacy first** — no accounts, no uploads, no telemetry by default; file paths are redacted in logs.

## Getting started

1. Grab the latest build from [Releases](https://github.com/huyangpahuo/Viora/releases/latest) and unzip it (ships with the .NET runtime — nothing else to install);
2. Launch Viora → **Plugin Market** → install a few styles;
3. Head to **Stylize**, drop in a picture, and create.

From source: install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) → `dotnet build Viora.sln -c Release` → `dotnet run --project src/Viora.App -c Release`. Publishing & packaging: [Publishing & Packaging](docs/04-发布与打包.md).

## Documentation

| Doc | Contents |
|---|---|
| [01 Feature overview](docs/01-功能总览.md) | every feature across the four pages |
| [02 Architecture & code layout](docs/02-架构与代码结构.md) | project layering (Mermaid), what every folder/file does |
| [03 Plugin development](docs/03-插件开发指南.md) | build a style plugin from scratch and publish it |
| [04 Publishing & packaging](docs/04-发布与打包.md) | dotnet publish (x64/x86), MSIX, Microsoft Store |
| [05 Settings & localization](docs/05-设置与本地化.md) | every setting explained, fonts & fallbacks, language packs |

## Plugin ecosystem

- Official plugin repository: [huyangpahuo/Viora-plugins](https://github.com/huyangpahuo/Viora-plugins) — the distribution source for all 55 styles and the entry point for third-party plugins (submit a PR);
- A plugin is just a folder (`plugin.json` manifest + compiled DLL); the market's zip is that folder compressed. Drop it into `plugins` to install, delete it to uninstall;
- Building your own style plugin: reference `Viora.PluginSdk`, write ~30 lines of entry code, and the algorithm can be any managed code — full tutorial in the [plugin guide](docs/03-插件开发指南.md).

## Acknowledgements

- [Font Awesome Free](https://fontawesome.com/) — every vector icon in the app (including the QQ / Discord / Telegram / GitHub brand logos), licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/);
- [.NET](https://dotnet.microsoft.com/) / WPF, [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet), Microsoft.Extensions.* (DI / Logging) — the app's infrastructure;
- **The app icon & mascot illustration** is fan art of **Kaedehara Kazuha** from *Genshin Impact*. The original author is unknown; it is used purely as non-commercial UI decoration. **If this infringes your rights, open an issue and I will remove or replace it immediately.**

## Contributing

Issues and PRs are welcome: keep the layering boundaries in client code (no WPF types in `Viora.Core`), sync both language packs for any UI string, and submit plugin listings as PRs to [Viora-plugins](https://github.com/huyangpahuo/Viora-plugins) (package + a `registry.json` entry).

## License

See [LICENSE](LICENSE).
