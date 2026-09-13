# Viora Architecture

> Companion to `00-inspection-and-plan.md`. This document describes the system as **actually built**.

## 1. Layered overview

```text
┌────────────────────────────────────────────────────────────┐
│ Viora.App (net8.0-windows, exe)                            │
│  Composition root: DI wiring, App.xaml, MainWindow,        │
│  global exception containment, import/export bridges,      │
│  plugin host start-up sequence                             │
├────────────────────────────────────────────────────────────┤
│ Viora.UI (net8.0-windows)                                  │
│  Shell (sidebar navigation + page cache), Pages (+VMs),    │
│  Design System (Themes/*.xaml tokens & control styles),    │
│  {loc:Translate} live localization bindings, ThemeManager, │
│  PresetCatalog, IFeatureRegistry, import/export proxies    │
├────────────────────────────────────────────────────────────┤
│ Viora.Features / Convert.Common / Convert.AnimeVector      │
│  Built-in feature = IBuiltInFeature registered through     │
│  the same IPluginContext seam plugins use. AnimeVector:    │
│  Preprocess → Smooth → Quantize → Consolidate →            │
│  ShadowBlock → EdgeInk (all managed, parallel, cancellable)│
├────────────────────────────────────────────────────────────┤
│ Viora.Core (net8.0 — no WPF reference)                     │
│  IImageProcessingStage / IImageConversionEngine /          │
│  IStylePreset / ISettingsService / ILocalizationService /  │
│  IPluginHost / IPluginContext / IVioraPlugin surface,      │
│  IImageBuffer (BGRA), VersionRange                         │
├────────────────────────────────────────────────────────────┤
│ Viora.PluginSdk (net8.0)                                   │
│  IVioraPlugin + VioraPluginBase — the ONLY assembly a      │
│  third-party plugin must reference                         │
├────────────────────────────────────────────────────────────┤
│ Viora.Infrastructure (net8.0-windows)                      │
│  JsonSettingsService (atomic writes), ImageConversionEngine│
│  (stage runner + progress mapping), AssemblyPluginHost +   │
│  collectible PluginLoadContext, PluginManifest, WIC        │
│  exporters (PNG/JPEG/BMP), ImportService (EXIF-aware),     │
│  AppPaths, FileLoggerProvider                              │
├────────────────────────────────────────────────────────────┤
│ Viora.Localization (net8.0)                                │
│  JsonLocalizationService — embedded en/zh-Hans packs,      │
│  en fallback, plugin string registration ("lang::Key")     │
└────────────────────────────────────────────────────────────┘
```

**Enforced boundary:** `Viora.Core`, `Viora.PluginSdk`, `Viora.Features` (preset/stage code), and
`Viora.Localization` contain **zero** `PresentationFramework`/`PresentationCore` references.
WPF types appear only in `Viora.UI`, `Viora.App`, and the adapter surfaces of `Viora.Infrastructure`
(decoders, exporters) — the layer the brief designates for "codec adapters".

## 2. Key decisions (Section 11 protocol)

| Decision | Alternatives considered | Chosen | Why / trade-off |
|---|---|---|---|
| Anime Vector algorithm | ONNX segmentation-assist; hybrid | **Classical CV pipeline** | Directly operationalizes the Style Analysis rules (palette collapse + hard shadows + shape simplification); zero model/licensing weight; fully offline. ONNX upgrade path = a plugin stage, no redesign. |
| MVVM toolkit | Hand-rolled ObservableObject | **CommunityToolkit.Mvvm 8.3** | Source-gen boilerplate removal; MIT; ecosystem default. |
| Plugin isolation | Out-of-process; AppDomain (gone) | **Collectible AssemblyLoadContext** | In-process unloading + dependency isolation. Trade-off: a plugin that leaks live types stays memory-resident but disabled (logged as "unload deferred"). |
| Localization format | RESX | **JSON packs** | Add-a-file language drop, no recompile, hot-swap friendly; key-coverage enforced by tests instead of compiler. |
| Settings persistence | Registry; per-section files | **Single versioned JSON** (`%LOCALAPPDATA%\Viora\settings.json`) | Atomic temp+replace writes; corrupt file backed up `.corrupt` and re-defaulted; migrations keyed on `SchemaVersion`. |
| Solution layout | Collapsed single project | **8 projects per brief §4** | Each enforces one mandated boundary; nothing added beyond the brief's tree. |

## 3. Image pipeline contract

- `IImageConversionEngine.ExecuteAsync(pipeline, source, parameters, previewQuality, progress, ct)`
  walks stages sequentially on the thread pool; each stage parallelizes its pixel loops and
  reports `StageProgress`, which the engine maps onto overall `PipelineProgress`.
- Cancellation: the engine checks the token between stages; stages check inside their loops
  (`Quantize`/`Consolidate`/`Duotone` throw `OperationCanceledException` promptly — covered by tests).
- Preview vs export: the UI runs the pipeline on a downsampled buffer (`PreviewMaxDimension`)
  while adjusting, and re-runs at full resolution on export (`ExportMaxDimension`).
- `IImageBuffer` is a plain BGRA byte grid — no framework types — so stages are unit-testable
  headlessly (see `AnimeVectorStageTests`).

## 4. Plugin system as built

1. **Discovery** — `%LOCALAPPDATA%\Viora.plugins`-equivalent `plugins/<id>/plugin.json`
   metadata-only scan; malformed folders are logged and skipped.
2. **Version check** — `RequiredHostVersion` range vs. entry-assembly version, **before** load;
   incompatible plugins surface as `Incompatible` with a localized message in the Plugins page.
3. **Load** — collectible `PluginLoadContext` per plugin; `Viora.Core`/`Viora.PluginSdk`
   unify with the host; plugin-private DLLs resolve from the plugin folder first.
4. **Initialize** — capability-checked `IPluginContext` (`convert.preset`, `export.format`,
   `ui.panel`, `localization.strings`); all host-boundary calls wrapped: a throwing plugin
   becomes `Failed` state + log, never a crash.
5. **Enable/Disable** — persisted in `Plugins.DisabledPlugins`; disable unloads when possible.
6. **Install/Uninstall** — `.vplugin` (zip with `plugin.json` at root); uninstall = shutdown +
   unload + delete folder (IO errors logged, retried next start).
7. **Sample lifecycle proof** — `samples/SamplePlugin` (Duotone preset) + `tools/build-sample-plugin.ps1`
   produce `DuotoneSample.vplugin`; install/enable/disable/uninstall round-trips in the UI.

## 5. What to revisit (deliberate, documented shortcuts)

- **SVG export**: pipeline computes region boundaries but raster output ships first; SVG writer
  is the next increment (boundaries already extracted in `ConsolidateStage`).
- **Cache settings**: enable/size persisted and wired to the path provider; the preview result
  cache itself lands with the SVG/perf pass.
- **Marketplace**: `InstallFromPackageAsync` accepts a local path; a future `InstallFromUriAsync`
  slots into `IPluginHost` without interface churn.
- **UI-scale setting** persists but applies per-window on restart (WPF layout scaling is not
  hot-swappable trivially); documented honestly in the settings page ("Applies immediately" copy
  only appears next to settings that genuinely are).
