# Viora — Phase 0 Inspection & Implementation Plan

> Produced per Section 1 of `Viora_Engineering_Prompt.md`. Every downstream decision references this document.
> Inspection date: 2026-09-07. All observations below come from actually reading the files/images, not from assumptions.

---

## 1. Repository Inventory

Workspace root `D:\WPF_learn\`:

| Item | Nature | Notes |
|---|---|---|
| `Viora/` | Git repo, this product | 1 commit ("Initial commit"), LICENSE = **AGPL-3.0**, contains only `LICENSE` + `Viora_Engineering_Prompt.md` before this Phase. No `.sln`, no source. |
| `Viora/Viora_Engineering_Prompt.md` | The engineering brief | Authoritative spec (this document's source). |
| `NavigationBar/` | Reference WPF project | `NavigationBar.sln` with two projects: `NavigationBar/` (class lib, the `MagicBar` control) and `DemoApp/` (demo exe). |
| `矢量二次元人物风格/DM_20260907214913_001.jpg` | **Anime Vector reference image** | 1400×1400 JPEG, ~114 KB. Copied into `Viora/assets/reference-anime-vector.jpg` during Phase 0. |

**Reference image location (exact):** `D:\WPF_learn\矢量二次元人物风格\DM_20260907214913_001.jpg` → now canonical at `Viora/assets/reference-anime-vector.jpg`.

**NavigationBar project location (exact):** `D:\WPF_learn\NavigationBar\` — source files: `NavigationBar/MagicBar.cs`, `NavigationBar/Themes/Generic.xaml`, `DemoApp/MainWindow.xaml`, `DemoApp/App.xaml`.

**Build environment:** `dotnet --list-sdks` → **8.0.405, 9.0.312, 10.0.201** (Windows x64, Git Bash shell, MSBuild via `dotnet build`). No CI config, no `.editorconfig`, no existing build scripts anywhere in the workspace.

**Toolchain decision (Section 3.1):** target **`net8.0-windows`** — the latest LTS installed, and identical to the reference project's framework, which eliminates any framework-mismatch ambiguity when consulting it. .NET 10 is current but non-LTS; .NET 9 is STS. NuGet packages to use: `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` (+ `Microsoft.Extensions.Logging.Abstractions`, a file/console sink via `Microsoft.Extensions.Logging.Debug` plus a small built-in file sink in `Viora.Infrastructure`), `Microsoft.Extensions.Configuration.Json` not needed — settings use `System.Text.Json` directly. All offline, all MIT-licensed.

---

## 2. Style Analysis — Anime Vector reference image

*Inspected visually and measured via pixel sampling (2 quantization passes; HSV computed per cluster). Image: two anime girls, waist-up, front-facing, over a diagonal-striped background.*

### 2.1 Composition
- Two figures occupy the central ~60% of the frame, symmetric diagonal balance (left figure white-haired, right figure red-haired), roughly knee-up framing.
- Large **negative-space diagonal bands** in the background run at ~30–45°; the figures are placed on the band boundaries so the background stripes read as deliberate graphic composition, not wallpaper.
- Silhouettes are **highly legible** — each figure's outline is a single unbroken closed shape against the striped background; you could cut either figure out with scissors and it would still read.

### 2.2 Palette (measured)
Six dominant clusters (coarse 24-step quantization, % of sampled pixels):

| Hex | Share | HSV | Role |
|---|---|---|---|
| `#1A1A2E`~`#181830` | ~20% | near-black, slightly blue | Background stripe 1 / garment shadows / line-art masses |
| `#F06060` (coral red) | ~19% | H≈0 S=0.60 V=0.94 | Background stripe 2 / right figure's hair |
| `#F0F0F0` (off-white) | ~16% | neutral | Skins' highlight tone, clothing base, background stripe 3 |
| `#0090C0` (cyan blue) | ~15% | H≈195 S=1.00 V=0.75 | Background stripe 4 / clothing accents |
| `#F0C018` (golden yellow) | ~8% | H≈47 S=0.90 V=0.94 | Accent blocks (aprons, hair clips, armbands) |
| `#F0D8D8` (pink-tinted white) | ~6% | S=0.10 | Skin mid-tone |

- **Warm/cool balance:** cool cyan vs. warm coral/yellow in near-equal measure — a deliberate **near-complementary pair** (cyan ↔ coral) with yellow as a tertiary accent. Saturation is uniformly **high** (0.6–1.0) except the near-black and off-white anchors.
- **Banding per region:** each major region (hair, skin, clothing) uses **2–3 flat tones** — e.g. white hair = off-white mass + yellow accent clip + soft blue-grey shadow sliver; skin = pink-white mid + slightly deeper pink shadow at the neck/collarbone. There is **no tonal gradient inside any region**; every tone change is a hard-edged shape.

### 2.3 Region decomposition
- **Hair:** 2 tones max + small accent shapes; the hair mass is decomposed into 5–8 large "petal" shapes with hard boundaries; individual strands are not drawn — shape boundaries *imply* strands.
- **Face/skin:** flat fill + at most 1 shadow tone; blush is a hard-edged pink ellipse; no gradient shading anywhere.
- **Eyes:** the most detailed element — 3–4 tones (dark base, iris color, white highlight dot, thin lash mass), but geometrically simplified: iris is a rounded rectangle-ish mass, no render-level detail.
- **Clothing:** color-blocking with 3–5 geometric patches per garment; patch boundaries follow fabric logic loosely but are decorative — they read as "design panels," not folds. Shadows on clothing are **hard-edged black-ish wedge shapes** (e.g., under the arm, under the chin) — classic "cel block" but simplified further into pure geometry.
- **Accessories:** headphones/goggles reduced to 2–3 stacked rounded rectangles + circles.

### 2.4 Outline treatment
- **Almost no drawn outlines.** Region separation is achieved by **color contrast alone** (white hair against black band; coral jacket against cyan stripe). Where a dark line exists (eye lash mass, a few garment seams), it is a **filled dark shape**, not a stroked line, and its color is the shared near-black `#1A1A2E`, not pure black.
- Line weight is therefore variable-by-design: "lines" are really thin color blocks.

### 2.5 Shadow/highlight treatment
- Shadows are **hard-edged geometric shapes** — wedges, triangles, parallelograms — with **0 feathering**. One shadow tone per region (occasionally two: black wedge + deeper color wedge on clothing).
- Highlights are rare and geometric (a white rectangle on hair, a dot in the eye).
- Light-source logic is **loose/decorative**: shadows appear where the composition needs darkening for contrast, not from a consistent single light. Background stripes ignore lighting entirely.

### 2.6 Edge/shape character
- Silhouette edges are **smooth large-radius curves** at the macro level (hair masses, shoulders) but every interior boundary is either a straight line or a single arc — **faceted, polygonal, "vector-tool" feeling**. No bezier-organic detail; detail is *removed*, not smoothed.
- Facial features vs. photoreal/anime baseline: nose is omitted or a single tick; mouth is a short 1-stroke line; ears often omitted; the face is essentially a flat region + eyes + mouth tick. Extreme simplification.

### 2.7 Style rules (must-have) vs. subject-specific (incidental)
**Style rules the output must satisfy to "read" as this style:**
1. Global color count collapses to a **small bounded palette** (roughly 6–10 colors), high saturation, anchored by one near-black and one near-white.
2. All interior tone changes become **hard-edged shapes** — zero soft gradients, zero photographic texture.
3. Regions are **coherent connected shapes** (a region should not be salt-and-pepper noise), boundaries smooth/simplified, detail abstracted into geometry.
4. Contrast structure preserved: dark anchoring masses (hair shadow, background bands) vs. light figure masses.
5. Facial detail reduced toward the geometric minimum (eyes/mouth survive as simplified masses, skin texture gone).

**Incidental to this particular image (must NOT be hardcoded into the algorithm):** the exact palette (coral/cyan/yellow), the diagonal background stripes, two-figure composition, headphones, poses. The algorithm must derive the palette **from the input image**.

### 2.8 Consequence for the algorithm (referenced in §5 below)
The reference is, in essence, **"posterized flat-color vector art with hard-edged shadow blocks and simplified silhouettes."** The dominant visual grammar is *color-region geometry*, which means: aggressive color quantization into a small palette + edge-preserving region smoothing + region-boundary simplification reproduces the style rules faithfully. A texture/gradient-preserving approach (e.g., style-transfer) would *violate* rules 2–3. See §10 for the full decision.

---

## 3. Design Language Extraction — NavigationBar project

*From `NavigationBar/Themes/Generic.xaml`, `NavigationBar/MagicBar.cs`, `DemoApp/MainWindow.xaml`.*

1. **Layout skeleton:** full-window dark canvas (`#222222`) with a single **floating bottom bar** (440×120, centered). The demo is single-surface; no sidebar/top-nav. Content region = everything above the bar.
2. **Navigation model:** flat `ListBox` of icon+label items; selected item is expressed by a **traveling "circle" indicator** (an 80×80 dark circle with a colored inner dot that slides under the selected item, `SelectedIndex * 80`, animated). Label text fades in on selection (`#00000000` → visible), icon shifts up 80px and darkens to `#333333`.
3. **Typography:** single font (system default), `FontSize=14`, `FontWeight=Bold` for labels. Hierarchy expressed by **color/opacity**, not size (disabled = 44-alpha black, selected = full `#333333`).
4. **Spacing system:** loose multiples of 20 (bar padding 20, bar top margin 40, item grid 80-wide per cell). Implied 8px-grid-compatible, but generous — spacious, not dense.
5. **Corner radius:** `CornerRadius=10` on the bar; circles (radius 40/34) for the indicator. Rounded, soft, "dock"-like.
6. **Color system:** window `#222222` (very dark grey), bar surface `#DDDDDD` (light grey) — a **strong light-on-dark contrast pair**; accent = `CadetBlue` inner dot; icon default `#44333333` (heavily transparent black), selected `#333333` opaque. Text-on-light uses near-black; no dark/light theme switching — the light bar sits on a dark window (a "floating island" pattern).
7. **Component styling patterns:** everything is flat (no drop shadows, no gradients); states are expressed via color + position animation; icons are vector `Geometry` glyphs from Jamesnet's `JamesIcon`.
8. **Motion:** `CubicEaseInOut` storyboards at **500 ms** for selection (icon shift, label fade, indicator slide); indicator uses `QuinticEaseInOut` 500 ms. Motion is a first-class part of the design language, not decoration.
9. **Overall feel:** **playful-minimal, spacious, animation-forward** — a "dock" aesthetic. Dense/utilitarian it is not.

**Translation to Viora Design System (spirit, not clone):** keep the dark anchored shell + light floating surfaces + rounded geometry + springy eased motion + opacity-based hierarchy; extend into a **left sidebar navigation** (an image tool needs a persistent, always-visible nav, not a bottom dock) with a large content canvas for the image viewport, and a parameter side panel. Accent moves from CadetBlue to a Viora-specific accent; the traveling-indicator motif returns as an animated selection pill in the sidebar. Full token table in `docs/design-system.md`.

---

## 4. Implementation Plan (nine points from the original brief)

### 4.1 Product architecture
Concept map exactly per brief §2: `Viora.App` (exe, composition root) hosts `Viora.UI` (views/VMs/design system), which consumes only `Viora.Core` contracts. Built-in features live in `Viora.Features.*` and register through the same `IPluginContext` seam that external plugins use — **built-ins are "plugins that ship in the box,"** so the Anime Vector preset proves the extensibility path instead of bypassing it. Infrastructure concerns (file I/O, plugin loading, settings persistence) are isolated in `Viora.Infrastructure`.

### 4.2 UI architecture
MVVM via `CommunityToolkit.Mvvm` (source-generator `ObservableProperty`/`RelayCommand`; choice justified in §10). Navigation: a `MainWindow` shell with a sidebar `ListBox` bound to a `NavigationItem` VM list; content region is a `ContentControl` over DI-resolved page VMs. No code-behind beyond `InitializeComponent` + event-to-VM plumbing that has no XAML-equivalent (drag & drop). All styling through merged `ResourceDictionary` token files; zero hardcoded colors/sizes in page XAML.

### 4.3 Plugin architecture
Per brief §6 verbatim: `plugin.json` manifest + `AssemblyLoadContext` isolation, discovery (metadata-only) → load → version check → initialize; enable/disable independent of load; host-boundary try/catch with "failed" state + localized error; capability-declared permissions; uninstall = unload + delete folder. Sample plugin under `samples/` proves the lifecycle.

### 4.4 Image conversion architecture
Pipeline contracts in `Viora.Core` exactly per brief §5.1. `IImageConversionEngine` walks an ordered stage list with `IProgress<PipelineProgress>` and `CancellationToken`. Buffers are a plain `IImageBuffer` (BGRA byte grid) — no WPF types in Core; `Viora.Infrastructure` adapts to/from WPF `BitmapSource` and file codecs. Chosen Anime Vector approach: **classical CV raster pipeline** (§10).

### 4.5 Localization architecture
`ILocalizationService` (Core) + JSON language packs (`en.json`, `zh-Hans.json`) in `Viora.Localization`, loaded at runtime and hot-swappable. XAML binding via a custom `TranslateExtension` markup extension bound to a `LocalizationManager` (INotifyPropertyChanged) so **language switches re-render live without restart**. Missing key → English fallback → visible `[key]` only in debug builds. Plugins register their own string tables via `IPluginContext`.

### 4.6 Settings architecture
`ISettingsService` with the nine strongly-typed sections from brief §9, persisted as a single versioned JSON document (`settings.json`) under `%LOCALAPPDATA%\Viora\` via `System.Text.Json` (source-gen serializers). Save is debounced 500 ms after change + explicit Save on settings page close. Every setting wired to real behavior (language, theme, log level, cache path/limit, preview quality, etc.).

### 4.7 Project structure
Exactly the Section 4 tree — 8 src projects + 3 test projects + `plugins/` + `assets/` + `docs/`. Justification: each project maps to one layer boundary the brief makes mandatory (WPF isolation, plugin SDK surface, feature hosting); collapsing any two would re-couple a boundary the brief explicitly forbids coupling. Nothing added beyond the brief's tree.

### 4.8 Development phases
Phases 0–9 as listed in brief §14, executed in order; each phase ends compiling and runnable. (This document completes Phase 0.)

### 4.9 Risks and technical trade-offs
| Risk | Mitigation |
|---|---|
| Anime Vector quality on real photos (classical CV limits) | Position as *stylization*, not AI art; parameterized strength; document expected inputs; preview-first UX |
| K-means/segmentation cost on large images | Preview pass at reduced resolution; full-res only on export; `CancellationToken` checks per stage; downsampling cap parameter |
| `AssemblyLoadContext` unloading pitfalls (cached types, event leaks) | Collectible ALC + strict host-boundary interfaces only; failed-unload falls back to "disabled, loaded" state (documented) |
| Live language switch complexity | Bind every string through `TranslateExtension`; smoke-test page sweep in Phase 3 |
| AGPL-3.0 license of the repo | All dependencies chosen MIT/Apache; no GPL-linked native libs (OpenCode pathological case avoided — pure managed CV) |
| Jamesnet.Wpf (reference project dep) | **Not** reused — analyzed only; Viora has its own design system |

---

## 5. Decision protocol record (Section 11 compliance)

Summaries here; full rationale lives in `docs/architecture.md` where noted.

1. **MVVM toolkit** — CommunityToolkit.Mvvm vs. hand-rolled `ObservableObject`. **Chosen: CommunityToolkit.Mvvm** (MIT, source generators cut boilerplate, industry-standard, no WPF coupling). Hand-rolled rejected: pure boilerplate cost with no benefit. *(speed-justified, revisit never — this is the ecosystem default)*
2. **Plugin isolation** — `AssemblyLoadContext` (collectible) vs. separate process per plugin vs. AppDomain (unavailable in .NET 8). **Chosen: collectible ALC.** Process isolation is over-engineering for v1 (IPC cost, complexity); ALC gives unload + version isolation in-process. Trade-off documented: a plugin that leaks its own types may fail to fully unload → falls back to disable-only.
3. **Localization format** — RESX vs. JSON. **Chosen: JSON.** Adding a language = dropping a file, no recompile (brief §8 explicitly values this); RESX compiles into assemblies and fights hot-swap. Cost: no tooling (acceptable; we ship a key-coverage test).
4. **Settings persistence** — single JSON doc vs. per-section files vs. registry. **Chosen: single versioned JSON** under local app data: atomic write via temp+rename, schema-versioned for migrations, trivially inspectable. Registry rejected (portability, diffability).
5. **Anime Vector algorithm** — see §6.

---

## 6. Anime Vector algorithm — approach decision

**Candidates evaluated (brief §5.3):**

| Criterion | A. Classical CV pipeline | B. ONNX segmentation-assisted | C. Hybrid (classical + optional model) |
|---|---|---|---|
| Maintainability | ✔ pure managed, no native deps | ✖ model hosting/versions | ✖ two codepaths |
| Performance | ✔ ~seconds, tunable | ✖ model download/inference cost | middle |
| Accuracy vs §2 style rules | ✔ directly implements rules 1–3 | ✔✔ region coherence | ✔ |
| Extensibility (future presets) | ✔ stages reusable | ✔ stages reusable | ✔ |
| Dependency/licensing | ✔ none (all managed) | ✖ model license + ONNX runtime (~100 MB) | middle |
| Offline | ✔ fully | ✖ unless model bundled (repo weight) | middle |

**Chosen: A — classical CV, raster-first, staged as:**

```text
Decode → Preprocess (orientation/size cap) → Edge-preserving smooth (iterated bilateral-like, managed)
      → Color quantization (k-means in Lab, k bounded, seeded from image histogram)
      → Region consolidation (connected components; merge micro-regions into neighbors)
      → Boundary simplification (marching-squares trace + Douglas–Peucker on region masks)
      → Shadow blocking (luminance-band remap into hard-edged tones per region, per §2.5)
      → Edge re-inking (dark-tinted stroke on high-contrast boundaries, per §2.4 — optional, default subtle)
      → Render raster + optional SVG path export (simplified polygons per color layer)
```

This is **not** a cartoon filter / B&W filter / canny-trace: it is palette collapse + shape simplification + hard-shadow blocking, i.e. a direct operationalization of §2.7's five style rules, and it satisfies every evaluation criterion. Segmentation-assist (B) is recorded as the future upgrade path via a plugin-provided stage — the pipeline contract admits it without redesign.

**Raster-first, SVG as derived export:** the reference's style is achievable purely in raster space; SVG paths are produced from the simplified region boundaries as a downstream representation (doubling as the "true vector" capability without a vector-first engine).

**Explicitly avoided per brief:** instagram-cartoon filter, B&W, pretrained style transfer, conventional cel-shading as the *core* (our shadow-blocking stage is geometric color-blocking, not luminance cel shading), generic low-poly, naive canny tracing.

---

## 7. Phase 0 acceptance check (brief §14 Phase 0)

- [x] Inspection complete (§1–§3), grounded in the actual files/image (§2 has measured data)
- [x] Tooling confirmed: .NET 8.0.405 SDK present
- [x] Nine-point plan (§4)
- [x] Anime Vector approach chosen with alternatives compared (§6)
