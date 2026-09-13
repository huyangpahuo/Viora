# Viora

**Create. Transform. Stylize.**

[简体中文文档](README.zh-CN.md)

Viora is a modular Windows desktop studio for image creation, transformation, and stylization,
built on WPF/.NET 8. Its first shipped capability is the **Anime Vector** conversion preset — a
flat geometric anime / vector illustration stylizer — and its plugin system is designed so every
future capability can be added without redesigning the application.

```text
                         Viora
                           │
              ┌────────────┴────────────┐
              │                         │
        Viora Core                 Feature System
              │                         │
    ┌─────────┼─────────┐       ┌───────┼────────┐
    │         │         │       │       │        │
   UI      Settings   i18n   Convert   Edit   Plugins
                              │
                              ├── Anime Vector (built-in)
                              ├── Duotone (sample plugin)
                              └── Your plugin
```

## Features

- **Anime Vector preset** — palette collapse into a small bounded set of flat colors, hard-edged
  shadow blocking, region consolidation and boundary simplification, optional dark edge ink.
  Built from a direct analysis of a real reference image (see `docs/00-inspection-and-plan.md`).
- **Full conversion workflow** — drag & drop or picker import, live preview, original/result
  split or side-by-side compare, parameter sliders with reset, re-run, progress and cancellation,
  PNG/JPEG/BMP export.
- **Plugin system** — manifest-based discovery, version-checked, isolated (collectible
  `AssemblyLoadContext`), install/enable/disable/uninstall from the UI, capability-scoped
  registration, error containment: a broken plugin never crashes the host.
- **Bilingual UI** — complete English and Simplified Chinese packs; language switches live
  without restart. Plugins ship their own strings via `lang::Key` entries.
- **Real settings** — nine persisted categories (general, appearance, language, image
  processing, export, plugins, performance, cache, privacy, developer) with observable effects:
  theme and log level apply instantly, resolution caps and quality flow into the pipeline.
- **Product infrastructure** — About (with honest update check), Help, Feedback (bug/feature/
  general routed to GitHub Issues), data-driven Community list, Sponsor, and User Agreement /
  Privacy / OSS licenses pages.
- **Local & offline** — all processing on your machine; no account, no upload, no telemetry by
  default; log paths are redacted by default.

## Screenshots

> UI captures (Home / Convert with Anime Vector on the reference image / Plugins / Settings)
> will be added to `assets/screenshots/` — the shell is dark, sidebar-grouped, accent-highlighted.

## Architecture (summary)

Eight projects enforce the layer boundaries (full detail in [`docs/architecture.md`](docs/architecture.md)):

| Project | Responsibility |
|---|---|
| `Viora.App` | Composition root: DI, global exception containment, bridges |
| `Viora.UI` | Shell, pages + MVVMs, design system, localization bindings |
| `Viora.Core` | Contracts only — **no WPF references** |
| `Viora.Features` | Built-in Anime Vector preset (registered like a plugin) |
| `Viora.PluginSdk` | The single reference third-party plugins need |
| `Viora.Infrastructure` | Engine, settings JSON, plugin host, codecs, logging |
| `Viora.Localization` | JSON language packs (en, zh-Hans) |
| `tests/*` | Core pipeline, feature stages, infrastructure round-trips |

## Image processing approach

The Anime Vector preset is a **classical computer-vision pipeline** chosen after comparing
classical / segmentation-assisted / hybrid approaches against the measured characteristics of
the reference image (six flat color masses, hard-edged shadows, no gradients, contrast-based
region separation — analysis in `docs/00-inspection-and-plan.md`):

```text
Preprocess (saturation) → Edge-preserving Smooth → k-means Quantize (bounded palette)
  → Region Consolidate (speckle merge) → Shadow Block (hard two-tone split)
  → Edge Ink (optional dark boundaries)
```

Every stage is managed code, parallelized, cancellable, and unit-tested for style-rule
properties (palette bound, region coherence) rather than brittle golden pixels.

## Plugin system

Install a `.vplugin` package from the Plugins page, toggle it, remove it — no restart.
Capabilities (`convert.preset`, `export.format`, `ui.panel`, `localization.strings`) gate what a
plugin may register. A working sample plugin ships in `samples/SamplePlugin` with a one-command
packaging script. Full guide: [`docs/plugin-development.md`](docs/plugin-development.md).

## Installation

**Run from source (recommended today):**

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. `dotnet build Viora.sln -c Release`
3. `dotnet run --project src/Viora.App -c Release`

**Portable mode:** create an empty `portable.marker` file next to `Viora.exe` to keep settings,
logs, cache and plugins beside the executable instead of `%LOCALAPPDATA%\Viora`.

## Usage

1. **Convert** page → drop an image (PNG/JPEG/BMP/GIF/TIFF/WebP).
2. Pick the **Anime Vector** preset; adjust *Palette size*, *Smoothing*, *Detail*,
   *Shadow blocking*, *Edge ink*, *Saturation* — the preview re-runs automatically at preview
   resolution while you drag.
3. Compare original/result by dragging the split handle (or side-by-side).
4. **Export** re-runs at full resolution and writes PNG/JPEG/BMP.

Settings → Language switches the UI instantly; Appearance toggles dark/light; Developer unlocks
full error details and log verbosity.

## Development

```bash
dotnet build Viora.sln            # build everything
dotnet test Viora.sln             # run the 29-test suite
dotnet run --project src/Viora.App
```

### Building the sample plugin

```powershell
powershell -File tools/build-sample-plugin.ps1
# → samples/SamplePlugin/DuotoneSample.vplugin  (install from the Plugins page)
```

### Plugin development

See [`docs/plugin-development.md`](docs/plugin-development.md) — manifest, capabilities, preset
stages, isolation model, packaging.

## Localization

Language packs are embedded JSON (`src/Viora.Localization/Assets/*.json`) with English fallback.
Contributing a language = adding a translated JSON file + registering the display name; no UI
rewrites. All user-facing strings are bound via `{loc:Translate Key}` so switches apply live.

## Roadmap

- [ ] SVG/vector export from the simplified region boundaries (boundaries already computed)
- [ ] Preview result cache with LRU eviction
- [ ] In-app documentation viewer
- [ ] Plugin marketplace (`InstallFromUriAsync`) and sandboxed plugin storage
- [ ] More built-in presets (the pipeline stage library is reusable)

## FAQ

**Is my image uploaded anywhere?** No. All processing is local; the privacy page documents the
exact behavior, and log paths are redacted by default.

**Why does the result look flat/posterized?** That is the style — the reference is flat
geometric vector art with hard shadows. Palette size and Detail parameters control how far the
simplification goes.

**A conversion runs slowly on huge images.** Preview-quality is used while adjusting; export
runs full-resolution. Caps in Settings → Image processing bound the cost.

**Can I add my own style?** Yes — that is the point. See the plugin guide; a preset is a
manifest + one pipeline builder + stage classes.

## Contributing

Issues and pull requests welcome. Please keep PRs aligned with the architecture boundaries
(no WPF types below `Viora.Infrastructure`), include tests for pipeline behavior, and keep both
language packs complete for any new strings.

## License

Viora is licensed under the **GNU AGPL-3.0** (see `LICENSE`). Third-party components and their
licenses are listed in-app (About → Third-party components) and in `docs/design-system.md`.

## Sponsor

Viora is free and open source. Sponsorship information lives on the in-app **Sponsor** page and
the GitHub Sponsors program — entirely optional, no paid gates.

## Community

The Community page is data-driven; real spaces (QQ/Discord/Telegram/forum) will be listed there
as they open.
