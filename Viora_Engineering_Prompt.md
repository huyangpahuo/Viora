# Viora — Engineering Execution Prompt for Coding Agent

## 0. How to Use This Document

You (the Coding Agent) are being asked to build **Viora**, a modular WPF desktop application for image creation, transformation, and stylization, whose first shipped capability is a **Flat Geometric Anime / Vector Illustration** conversion preset.

This document is a complete engineering brief. It does not remove or soften any requirement from the original product brief — it expands each one with concrete architecture, interfaces, constraints, and phased execution steps so you can work independently, with minimal need for clarification.

**You must not skip Section 1 (Mandatory Inspection Phase).** Every architectural and stylistic decision downstream depends on what you find there. Do not proceed to substantial implementation until Section 1's outputs exist.

Where this document says "decide" or "choose," you are expected to reason explicitly about trade-offs (see Section 11 — Engineering Decision-Making Protocol) rather than pick the first idea that compiles.

---

## 1. Mandatory Inspection Phase (Phase 0 — Do This First)

Before writing implementation code, inspect the actual project folder. Do not assume anything about the reference style or the UI reference from this text alone — the written description is a guide to what to look for, not a substitute for looking.

### 1.1 Inventory the repository

Enumerate and record:
- All files/folders already present (existing source, `.sln`/`.csproj` files, assets, config, `.git` history if any)
- The exact location and filename(s) of the **Anime Vector reference image(s)**
- The exact location of the **NavigationBar** WPF project (`.sln`/`.csproj`, XAML files, resource dictionaries, code-behind)
- Target framework(s) currently in use (`net8.0-windows`, etc.), NuGet packages already referenced, and SDK availability in the build environment
- Any existing build scripts, CI config, or `.editorconfig`/style rules

### 1.2 Analyze the reference image(s) — do not skip

Open and visually inspect the reference image(s) directly (view the file; do not infer from the filename or from general knowledge of "anime art"). Produce a written **Style Analysis** covering:

1. **Composition** — subject framing, negative space usage, silhouette clarity.
2. **Palette** — extract the dominant colors (approximate hex or HSV clusters), how many distinct color "bands" appear per major region (hair, skin, clothing), whether the palette is warm/cool/high-saturation/muted, and how colors relate to each other (complementary, analogous, etc.).
3. **Region decomposition** — how hair, face/skin, eyes, clothing, accessories, background are each handled: flat fill vs. multi-tone, how many shade steps per region.
4. **Outline treatment** — line weight, line color (pure black vs. dark tinted), where outlines are present vs. implied by color contrast alone.
5. **Shadow/highlight treatment** — are shadows hard-edged geometric shapes or soft gradients? How many shadow tones? Is there a consistent "light source" logic, or purely decorative shape-based shading?
6. **Edge/shape character** — are silhouette edges smooth curves, faceted/polygonal, or a mix? How simplified are facial features (eyes, nose, mouth) relative to a photorealistic or standard-anime baseline?
7. **What must be preserved for the result to "read" as this style** vs. what is incidental to this particular reference image (i.e., separate the *style rules* from the *subject-specific content*).

This analysis becomes the functional specification for the Anime Vector preset's algorithm (Section 5) and must be referenced, not re-guessed, when implementing it.

### 1.3 Analyze the NavigationBar reference project

Inspect the actual XAML, resource dictionaries, styles, templates, and code-behind. Produce a written **Design Language Extraction** covering:

1. **Layout skeleton** — how the window is composed (sidebar + content, top nav + content, overlay panels, etc.), region proportions, resizing/collapsing behavior if any.
2. **Navigation model** — how navigation items are structured, selected-state handling, icon+label conventions, whether it's a flat list, grouped, or hierarchical.
3. **Typography** — font family(ies), weight scale, size scale, how hierarchy is expressed (size vs. weight vs. color).
4. **Spacing system** — base unit, margin/padding conventions, whether an 4px/8px grid or similar is implied.
5. **Corner radius conventions** — values used on buttons, cards, containers.
6. **Color system** — background layers (window/surface/elevated-surface), accent color(s), text color hierarchy (primary/secondary/disabled), border/divider colors, and whether light/dark theming exists or is implied.
7. **Component styling patterns** — how buttons, toggles, inputs, and list items are styled (flat vs. elevated, hover/pressed states, iconography style).
8. **Motion/animation** — transition durations/easing if present (hover, selection, panel open/close).
9. **Overall interaction feel** — is it dense/utilitarian, spacious/premium, playful, minimal, etc.

**Do not copy NavigationBar's UI verbatim.** Use this extraction to define a **Viora Design System** (Section 6) that is clearly related in spirit but has its own identity, product-appropriate to an image-editing/creative tool rather than a generic nav demo.

### 1.4 Phase 0 Deliverable

Before writing feature code, produce (as a markdown doc committed to `docs/`, e.g. `docs/00-inspection-and-plan.md`):

1. Repository inventory summary
2. Style Analysis (from 1.2)
3. Design Language Extraction (from 1.3)
4. A concise implementation plan covering exactly these nine points from the original brief:
   1. Product architecture
   2. UI architecture
   3. Plugin architecture
   4. Image conversion architecture
   5. Localization architecture
   6. Settings architecture
   7. Project structure
   8. Development phases
   9. Risks and technical trade-offs
5. Your chosen technical approach for the Anime Vector algorithm, with the comparison of alternatives required by Section 11, and justification tied directly to the Style Analysis.

Only after this document exists should Phase 1+ implementation begin.

---

## 2. Product Vision (authoritative — unchanged from brief)

Viora is a **real, maintainable, commercial-quality Windows desktop product**, not a one-off demo. The current image stylization feature is the *first* capability of a platform, structured conceptually as:

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
                              ├── Anime Vector
                              ├── Future Preset
                              └── Future Preset
```

The architecture must let new image-processing features be added without redesigning the application. Every subsequent section exists to make this concrete and buildable — not just aspirational.

---

## 3. Technology & Layered Architecture

### 3.1 Stack
- C# / .NET (choose the latest LTS or current stable SDK available in the build environment — record the chosen version and why in the Phase 0 doc)
- WPF as the **shell and host only** — it must not contain image-processing logic
- MVVM as the presentation pattern (a lightweight MVVM toolkit such as CommunityToolkit.Mvvm is acceptable; justify the choice per Section 11 rather than defaulting silently)
- Dependency Injection container (e.g., `Microsoft.Extensions.DependencyInjection`) wired at `App.xaml.cs` composition root
- `Microsoft.Extensions.Logging` (or equivalent) for structured logging
- Async/await + `IProgress<T>` + `CancellationToken` throughout the processing pipeline
- JSON-based configuration/settings persistence (e.g., `System.Text.Json`) unless a stronger reason emerges during inspection

### 3.2 Layer responsibilities (conceptual → concrete)

```text
Presentation           Viora.UI            Views (XAML), ViewModels, converters, design system resources
        │
Application            Viora.App           Composition root, DI wiring, navigation shell, app lifecycle
        │
Domain / Features       Viora.Core          Interfaces & contracts: IImageProcessingService, IPluginHost,
                        Viora.Features       ISettingsService, ILocalizationService, IExportService, etc.
                                              Concrete built-in feature implementations (Anime Vector preset)
        │
Plugin SDK              Viora.PluginSdk     Public contracts third-party/internal plugins implement against
        │
Infrastructure           Viora.Infrastructure  File I/O, image codec adapters, plugin loader (AssemblyLoadContext),
                                              settings file persistence, logging sinks
```

**Hard rule:** `Viora.Core`/`Viora.Features` must have **no reference to `PresentationFramework`/WPF types**. If a feature needs to expose UI, it exposes a `UserControl`-producing factory or a ViewModel-shaped contract consumed by `Viora.UI`, never raw WPF logic embedded in the feature itself. This is what makes "WPF must not be tightly coupled to image-processing logic" enforceable rather than aspirational.

---

## 4. Project / Directory Structure

```text
Viora/
├── Viora.sln
├── src/
│   ├── Viora.App/                 # WPF executable, composition root, App.xaml, navigation shell
│   ├── Viora.UI/                  # Views, ViewModels, Design System (styles/templates/resources), converters
│   ├── Viora.Core/                # Domain interfaces/contracts, shared models, pipeline abstractions
│   ├── Viora.Features/            # Built-in feature implementations
│   │   ├── Convert.AnimeVector/   # The Anime Vector stylization implementation
│   │   └── Convert.Common/        # Shared conversion pipeline infrastructure used by presets
│   ├── Viora.PluginSdk/           # Public plugin contracts (versioned, NuGet-packable in principle)
│   ├── Viora.Infrastructure/      # File I/O, plugin loading, settings persistence, logging setup
│   └── Viora.Localization/        # Resource files / JSON language packs + ILocalizationService impl
├── plugins/                       # Runtime plugin drop-in folder (empty by default, gitignored contents)
├── assets/                        # Reference image(s), icons, design tokens exported from inspection
├── docs/
│   ├── 00-inspection-and-plan.md
│   ├── architecture.md
│   ├── plugin-development.md
│   └── design-system.md
├── tests/
│   ├── Viora.Core.Tests/
│   ├── Viora.Features.Tests/
│   └── Viora.Infrastructure.Tests/
└── README.md
```

Do not create additional projects purely for the sake of "more structure." Every project above must have one clear responsibility; if inspection reveals a simpler structure is sufficient for v1 without blocking extensibility, document that decision in `docs/architecture.md` rather than defaulting to the maximal layout.

---

## 5. Image Conversion Architecture

### 5.1 Pipeline contract (in `Viora.Core`)

```csharp
public interface IImageProcessingStage
{
    string Name { get; }
    Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken);
}

public sealed class ImageProcessingContext
{
    public required IImageBuffer Source { get; init; }
    public IImageBuffer? Working { get; set; }
    public IReadOnlyDictionary<string, object> Parameters { get; init; }
    public IList<IShapeRegion> Regions { get; } // populated by segmentation stages
    // ... additional shared state stages read/write
}

public interface IStylePreset
{
    string Id { get; }              // stable identifier, e.g. "builtin.anime-vector"
    string DisplayNameKey { get; }  // localization key, not raw text
    IReadOnlyList<IPresetParameter> Parameters { get; }
    IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters);
}
```

A **preset** (built-in or plugin-provided) composes an ordered list of stages. The engine (`Viora.Core`'s `IImageConversionEngine`) executes the pipeline asynchronously, reporting per-stage progress and supporting cancellation, and never assumes a fixed stage list — new stage types can be introduced without changing the engine.

### 5.2 Conceptual pipeline (from brief, to be mapped onto concrete stage implementations)

```text
Input Image → Preprocessing → Analysis/Segmentation → Color Reduction
    → Shape Simplification → Stylization Pipeline → Render/Vector Representation
    → Preview → Export
```

### 5.3 Anime Vector preset — implementation approach

Before implementing, use Section 11's decision protocol to compare at least these candidate approaches against the Style Analysis from 1.2, and document the choice in `docs/00-inspection-and-plan.md`:

- **Classical CV pipeline**: color quantization (k-means or median-cut in a perceptual color space) + edge-preserving smoothing (e.g., bilateral/guided filter) + contour extraction (e.g., OpenCV via a .NET binding, or a managed alternative) + polygon simplification (e.g., Douglas–Peucker) + region fill reconstruction, optionally exported as SVG paths.
- **Segmentation-assisted pipeline**: lightweight local model (e.g., an ONNX Runtime-hosted segmentation model) to separate hair/skin/clothing/background before quantization, improving region coherence over pure color-space clustering.
- **Hybrid**: classical preprocessing + optional local-model-assisted region proposals, falling back gracefully to pure classical CV when no model is available/licensed for redistribution.

Evaluate each against: maintainability, performance (must not block the UI thread — see Section 9), accuracy relative to the actual reference image characteristics identified in 1.2, extensibility for *future* presets, dependency/licensing cost of any native or model dependency, and offline support (Viora should not require a network call to convert an image). Prefer raster-first output with SVG/vector export treated as a downstream representation, unless inspection of the reference clearly demands a true vector pipeline from the start — justify whichever you pick.

**Explicitly out of scope / must not be the implementation:** a cartoon Instagram-style filter, a black-and-white filter, a generic pretrained "anime style transfer" model without the deliberate geometric color-blocking described in the Style Analysis, conventional cel-shading, generic low-poly faceting, or naive Canny-edge image tracing. If your chosen approach reduces to one of these, it has not satisfied the requirement — revisit.

---

## 6. Plugin System & SDK

### 6.1 Plugin contract (`Viora.PluginSdk`)

```csharp
public interface IVioraPlugin
{
    PluginMetadata Metadata { get; }
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken);
    Task ShutdownAsync(CancellationToken cancellationToken);
}

public sealed record PluginMetadata(
    string Id,                  // stable, unique, e.g. "com.author.pluginname"
    string DisplayName,
    Version Version,
    string Author,
    string Description,
    Uri? Homepage,
    Uri? Repository,
    VersionRange RequiredHostVersion,        // compatibility/version check
    IReadOnlyList<string> Capabilities,      // e.g. "convert.preset", "ui.panel", "export.format"
    IReadOnlyList<PluginDependency> Dependencies);

public interface IPluginContext
{
    void RegisterPreset(IStylePreset preset);
    void RegisterExporter(IImageExporter exporter);
    void RegisterSettingsPage(ISettingsPageDescriptor page);
    ILogger Logger { get; }
    // capability-scoped services only — plugins do not get raw filesystem/registry access by default
}

public interface IPluginHost
{
    Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync();
    Task<PluginLoadResult> LoadAsync(string pluginId);
    Task UnloadAsync(string pluginId);
    Task EnableAsync(string pluginId);
    Task DisableAsync(string pluginId);
    Task<InstallResult> InstallFromPackageAsync(string packagePath);
    Task UninstallAsync(string pluginId);
}
```

### 6.2 Lifecycle & isolation

- **Discovery**: scan `plugins/<pluginId>/` for a manifest (`plugin.json`) + assembly; do not eagerly load code during discovery, only metadata.
- **Load**: load into an isolated `AssemblyLoadContext` per plugin so a plugin can be unloaded/updated without restarting the host, and so plugin dependency versions don't collide with the host's.
- **Version/compatibility check**: compare `RequiredHostVersion` against the running host version *before* calling `InitializeAsync`; refuse to load and surface a clear, localized error if incompatible.
- **Enable/disable**: independent of load — a disabled plugin's contributed presets/UI are hidden but its assembly may remain loaded or be unloaded, per your chosen trade-off (document it).
- **Error containment**: wrap `InitializeAsync`/pipeline stage execution from plugin-provided code in try/catch at the host boundary; a throwing plugin must not crash Viora — log it, mark the plugin as "failed," and surface a user-facing, non-technical message.
- **Permissions/capabilities**: a plugin declares `Capabilities` in its manifest; the host only grants the corresponding registration methods on `IPluginContext` — this is the seam for a future permission system without requiring a redesign.
- **Uninstall/removal**: unload (if loaded), then delete the plugin's folder; never delete user data outside the plugin's own sandboxed storage.

### 6.3 Plugin management UI requirements (unchanged from brief, restated as acceptance-testable)
- List installed plugins with: name, version, author, description, enabled/disabled state, website/homepage link, repository link where available
- Enable/disable toggle per plugin (immediate effect, no restart required unless technically unavoidable — document if it is)
- Import/install from a package file
- Remove/uninstall with confirmation
- Clear error state display for plugins that failed to load, with access to the underlying log entry (not a raw stack trace in the primary view)
- Architecture must leave room for a future marketplace (i.e., `InstallFromPackageAsync` should not assume "local file only" as a permanent constraint — a future `InstallFromUriAsync` should be addable without interface churn)

---

## 7. UI Design System ("Viora Design System")

Do not hardcode this section's color/spacing values — they are placeholders illustrating *how* to encode what you find in Section 1.3. Replace them with your actual extraction.

### 7.1 Design tokens (define as WPF `ResourceDictionary` values, not literals scattered in XAML)
- **Color roles**: `Color.Background`, `Color.Surface`, `Color.SurfaceElevated`, `Color.Accent`, `Color.Text.Primary`, `Color.Text.Secondary`, `Color.Text.Disabled`, `Color.Border`, `Color.Danger`, `Color.Success` — each derived from the NavigationBar extraction, not invented independently.
- **Typography scale**: named styles (`Typography.Title`, `Typography.Heading`, `Typography.Body`, `Typography.Caption`) built from the extracted font family/weights.
- **Spacing scale**: a small set of spacing tokens (e.g., 4/8/12/16/24/32) derived from the observed grid.
- **Corner radius tokens**: `Radius.Small`, `Radius.Medium`, `Radius.Large` matching observed conventions.
- **Elevation/shadow tokens** if NavigationBar uses any depth cues.
- **Motion tokens**: standard durations/easing for hover, selection, panel transitions.

### 7.2 Principles
1. **Consistency over novelty** — every screen reuses the same token set; no per-page one-off colors or radii.
2. **Hierarchy through restraint** — typography weight/size and color establish hierarchy before resorting to borders/boxes.
3. **Respect the reference, don't clone it** — layout skeleton and navigation *pattern* may closely follow NavigationBar; iconography, accent usage, and content-area composition should be adapted to an image-editing tool (large canvas/preview area, parameter side panel, before/after comparison affordances).
4. **State clarity** — every interactive control has explicit hover/pressed/selected/disabled visuals defined once as a style, reused everywhere.
5. **No prototype tells** — no default WPF `Button`/`ListBox` chrome left unstyled; no placeholder Lorem Ipsum shipped in the final milestone; no debug-only elements visible in Release configuration.

### 7.3 Information architecture

Design the actual navigation structure based on the product's real surface area (Home, Image▸Convert/Edit/Process, Plugins, Settings, About/Help/Feedback/Community/Sponsor/Legal) rather than defaulting to "everything in the sidebar." Group product-infrastructure entries (About, Help, Feedback, Community, Sponsor, Legal) sensibly — e.g., a secondary/footer navigation cluster distinct from primary feature navigation — and document the chosen IA in `docs/design-system.md` with reasoning for why it stays legible as features grow.

---

## 8. Localization Architecture

- `ILocalizationService` in `Viora.Core`, implemented in `Viora.Localization`, exposing at minimum `string GetString(string key)` / a `Binding`-friendly indexer for XAML (e.g., a markup extension `{loc:Translate Key=Nav.Home}`).
- Language packs as external resource files (RESX or JSON — choose per Section 11 and justify; JSON has the advantage of not requiring recompilation to add a language, which matters given "additional languages without large-scale UI rewrites").
- **No hardcoded user-facing strings** anywhere in `Viora.UI`, `Viora.Features`, or `Viora.PluginSdk`-facing surfaces — this includes navigation labels, settings, dialogs, error messages, plugin management UI, help, about, legal pages, feedback, and community sections.
- Initial languages: **English**, **Simplified Chinese** — both must be complete (no missing-key fallback visible in normal usage) for every screen in the Definition of Done (Section 13).
- Language switching should apply without requiring an app restart if practically achievable; if not achievable in v1, document why and require at minimum an explicit "restart to apply" flow rather than a silently-ignored setting.
- Plugins must be able to ship their own language resources for any UI/text they contribute (`IPluginContext` should expose a way to register plugin-scoped localized strings).

---

## 9. Settings Architecture

- `ISettingsService` with typed, strongly-named settings sections (not a single loose dictionary): `GeneralSettings`, `AppearanceSettings`, `LanguageSettings`, `ImageProcessingSettings`, `ExportSettings`, `PluginSettings`, `PerformanceSettings`, `CacheSettings`, `PrivacySettings`.
- Persisted to a JSON file under the user's local app data folder; load on startup, save on change (debounced) or on explicit "Apply/Save," per your UX decision — document it.
- Settings changes must have real, observable effect — no settings control may be a non-functional placeholder in the Definition of Done.
- Settings UI organized by the categories above, each independently navigable (not one giant scrolling page unless inspection of NavigationBar's patterns specifically supports that as the right pattern — justify).

---

## 10. Product Infrastructure Requirements (all required — unchanged from brief)

Each of the following must exist as a real, navigable screen/section, localized, and reachable from the information architecture defined in Section 7.3:

| Section | Must contain |
|---|---|
| **About** | App name, version, description, author/developer info, GitHub project link, license info, third-party component/attribution list, update entry point (may be a manual "check for updates" placeholder if no update server exists yet — must be honest about its current behavior, not fake) |
| **Plugins** | Per Section 6.3 |
| **Settings** | Per Section 9 |
| **Help** | Entry points to online docs/FAQ/GitHub docs/official site; architecture must support future in-app documentation; localized |
| **Feedback** | Distinguishes bug report / feature request / general feedback; links to GitHub Issues or equivalent |
| **Community** | Extensible list of destinations (QQ group, Discord, Telegram, website, forum) — do not hardcode for a single platform; empty/placeholder entries are acceptable if real links aren't available yet, but the list must be data-driven, not UI-hardcoded per platform |
| **Sponsor/Support** | Presented as part of the product's normal information architecture, not an intrusive popup/banner |
| **User Agreement / Privacy** | Access to User Agreement, Privacy Policy, OSS licenses, third-party notices as normal pages |

---

## 11. Engineering Decision-Making Protocol

For every technically significant component — at minimum: the Anime Vector algorithm, the plugin isolation/loading strategy, the MVVM toolkit choice, the localization resource format, and the settings persistence format — you must, before committing:

1. Name at least two viable approaches.
2. Evaluate each against: maintainability, performance, accuracy (where applicable), extensibility, complexity, dependency cost, deployment considerations, offline support, and licensing implications.
3. State the chosen approach and why, in `docs/architecture.md` or `docs/00-inspection-and-plan.md`.
4. Flag anything you chose primarily for implementation speed that should be revisited later, so it doesn't silently calcify into permanent architecture.

Do not present only one option as if no alternative was considered, and do not over-engineer components with no near-term extensibility need (Section 19 of the original brief — "practical engineering").

---

## 12. Performance, Responsiveness, Error Handling, Logging

### 12.1 Performance
- All image processing runs off the UI thread; the UI thread only marshals progress/results back via dispatcher.
- Support cancellation of an in-flight conversion.
- Provide a lower-resolution/faster "preview quality" pass distinct from "full quality" export processing, where the pipeline design allows it.
- Consider memory footprint for large images (avoid redundant full-resolution copies held simultaneously without reason); consider a preview/result cache with a sane eviction policy.

### 12.2 Error handling
Handle, with user-friendly messaging (not raw exceptions) and detailed logs behind the scenes:
- Invalid images, unsupported formats, corrupted files, unusually large files
- Plugin loading failures, plugin version incompatibility
- Failed conversions, export failures
- Missing resources, invalid settings, unexpected processing exceptions
- A "developer/debug mode" (behind a settings toggle or build config) may expose full diagnostic detail; normal users never see raw stack traces by default.

### 12.3 Logging
- Structured logging via `Microsoft.Extensions.Logging` abstractions, with a file-based sink under local app data by default.
- Cover: application errors, plugin errors, image processing failures, export problems, initialization problems.
- Must not log sensitive user data unnecessarily (e.g., don't log full file system paths containing usernames beyond what's needed for diagnostics — use judgment and document the policy).
- Log verbosity must be controllable via settings (e.g., Info by default, Debug/Trace opt-in).

---

## 13. Testing Strategy

Automated tests should exist where they provide real value — not padding:

- **`Viora.Core.Tests`**: pipeline stage composition/execution ordering, cancellation propagation, settings serialization round-trips, localization key resolution/fallback behavior.
- **`Viora.Features.Tests`**: individual Anime Vector pipeline stages tested with deterministic inputs (e.g., a small synthetic test image) verifying expected properties (e.g., "color count after quantization stage is within the configured bound") rather than pixel-perfect golden-image matching, which is brittle.
- **`Viora.Infrastructure.Tests`**: plugin manifest parsing, version-compatibility checks, plugin load/unload isolation behavior (can use a minimal test plugin fixture), settings file read/write, export functionality for each supported format.
- Explicitly avoid tests that assert trivial framework behavior or exist only to inflate a test count.

---

## 14. Development Phases & Task Breakdown

Each phase must leave the codebase in a compiling, runnable, demonstrable state — no phase should end with a broken build.

### Phase 0 — Inspection & Planning
- [ ] Complete Section 1 inspection and produce `docs/00-inspection-and-plan.md`
- [ ] Confirm target .NET SDK/tooling available in the environment
- **Acceptance criteria:** the planning doc exists, references concrete observations from the actual reference image and NavigationBar project (not generic statements), and names the chosen Anime Vector technical approach with justification.

### Phase 1 — Solution Skeleton & Core Contracts
- [ ] Create solution/projects per Section 4
- [ ] Define core interfaces: `IImageProcessingStage`, `IImageConversionEngine`, `IStylePreset`, `ISettingsService`, `ILocalizationService`, `IPluginHost`, `IVioraPlugin` (empty/minimal implementations acceptable)
- [ ] Wire up DI composition root in `Viora.App`
- **Acceptance criteria:** solution builds; app launches to an empty/shell window with DI-resolved services logging startup.

### Phase 2 — UI Shell & Design System
- [ ] Implement design tokens (Section 7.1) as resource dictionaries
- [ ] Implement primary navigation shell + information architecture (Section 7.3)
- [ ] Implement Home, Settings (empty categories), About, Help, Feedback, Community, Sponsor, Legal as navigable but minimally-populated screens
- **Acceptance criteria:** every product-infrastructure entry from Section 10 is reachable via navigation and visually consistent with the design tokens; no default unstyled WPF controls remain visible.

### Phase 3 — Localization Foundation
- [ ] Implement `ILocalizationService` + XAML binding mechanism
- [ ] Populate English + Simplified Chinese resources for all Phase 2 screens
- [ ] Add language switch in Settings
- **Acceptance criteria:** switching language updates all Phase 2 screens with no missing-key fallbacks visible.

### Phase 4 — Settings Persistence
- [ ] Implement `ISettingsService` with real JSON persistence for all categories in Section 9
- [ ] Wire at least one setting per category to observable real behavior (e.g., language setting actually changes UI language; cache setting actually affects a cache path/size)
- **Acceptance criteria:** settings survive app restart; no settings control is a non-functional placeholder.

### Phase 5 — Plugin Infrastructure
- [ ] Implement `IPluginHost`, manifest format (`plugin.json`), `AssemblyLoadContext`-based isolated loading
- [ ] Implement Plugins management screen per Section 6.3
- [ ] Build one trivial internal "sample plugin" (can live in `tests/` or a `samples/` folder, not shipped in `plugins/` by default) to prove the load/enable/disable/unload/uninstall lifecycle end-to-end
- **Acceptance criteria:** the sample plugin can be installed, enabled, disabled, and removed through the UI; a deliberately broken/incompatible sample plugin fails gracefully with a user-friendly error and a corresponding log entry, without crashing the host.

### Phase 6 — Image Conversion Engine + Anime Vector Preset
- [ ] Implement `IImageConversionEngine` executing an ordered `IImageProcessingStage` list asynchronously with progress + cancellation
- [ ] Implement the chosen Anime Vector pipeline stages per Section 5.3, informed directly by the Phase 0 Style Analysis
- [ ] Implement Import (drag & drop + file picker), Original/Result preview, before/after comparison, zoom/pan, parameter controls with reset, re-run, export (per Section 15 formats) — full workflow per Section 15's conceptual flow
- **Acceptance criteria:** a user can import a real photo/illustration, select the Anime Vector preset, adjust at least one meaningful parameter, see a preview update, and export a result file; long-running conversion does not freeze the UI and can be cancelled.

### Phase 7 — Error Handling, Logging, Hardening
- [ ] Implement structured logging sinks and the developer/debug diagnostic mode
- [ ] Implement user-friendly error surfaces for every failure category in Section 12.2
- [ ] Pass invalid/corrupted/oversized test images through the pipeline and confirm graceful handling
- **Acceptance criteria:** none of the failure categories in Section 12.2 crash the app or leak a raw stack trace to a normal user.

### Phase 8 — Testing & Documentation
- [ ] Implement the test suites described in Section 13
- [ ] Write `docs/architecture.md`, `docs/plugin-development.md`, `docs/design-system.md`
- [ ] Write the full README per Section 16
- **Acceptance criteria:** tests pass in CI/locally; README and docs accurately describe the actually-built system (not the aspirational brief).

### Phase 9 — Polish Pass
- [ ] Visual QA against the Viora Design System tokens across every screen
- [ ] Verify full i18n coverage (no missing keys) across every screen including plugin-contributed and error-state UI
- [ ] Final Definition of Done review (Section 17)

---

## 15. Image Conversion UX (unchanged, restated as acceptance-testable)

```text
Import Image → Preview → Select Preset → Adjust Parameters → Preview Result
    → Compare Original / Result → Export
```

Must support: drag & drop import, file picker import, original preview, result preview, before/after comparison, zoom, pan, preset selection, parameter controls, reset parameters, re-run conversion, export, progress indication, cancellation. Export formats exposed in the UI must match what the internal representation actually supports (PNG and other practical raster formats at minimum; SVG/vector export only if the chosen pipeline genuinely produces a vector representation — do not expose export options the pipeline can't actually fulfill).

---

## 16. README Requirements

Produce a full, professional `README.md` (not a placeholder) covering: Introduction, Features, Screenshots (reference actual captured screenshots once the UI exists), Architecture (summarize Sections 3–4), Image Processing (summarize Section 5 and the chosen approach), Plugin System (summarize Section 6, link to `docs/plugin-development.md`), Installation, Usage, Development, Building, Plugin Development, Localization, Roadmap, FAQ, Contributing, License, Sponsor, Community. Adjust structure as needed but do not ship a minimal stub.

---

## 17. Definition of Done

The project is not done because it compiles. It is done when all of the following are true simultaneously:

- Functioning WPF app with coherent Viora-branded UI following the design system defined in Phase 0/Section 7
- Full navigation across the information architecture from Section 7.3
- Settings genuinely persisted and functional (Section 9)
- i18n foundations complete for English + Simplified Chinese with no missing keys anywhere in the shipped UI
- About, Help, Feedback, Community, Sponsor/Support, User Agreement/Privacy sections all present and reachable
- Plugin infrastructure functional end-to-end (install/enable/disable/remove) with a demonstrated sample plugin
- Working image conversion workflow: import → preview → preset selection → parameter adjustment → processing feedback → export, using the Anime Vector system preset built from the actual reference-image analysis
- Error handling and logging implemented per Section 12
- A professional README and supporting `docs/` per Sections 16 and 14 Phase 8
- The codebase reflects the layered architecture in Section 3 with no significant image-processing logic embedded in `MainWindow.xaml.cs`, other UI code-behind, static global classes, or a single giant manager class

---

## 18. Constraints Checklist (must hold throughout, not just at the end)

1. Anime Vector is a feature, not the entire application — architecture must show this at every layer.
2. Viora must be plugin-oriented — new capabilities are added as plugins/features, not `if feature == X` branches in the core or UI shell.
3. WPF must not become tightly coupled to image-processing logic (`Viora.Core`/`Viora.Features` have no `PresentationFramework` reference).
4. The NavigationBar project must be analyzed as a visual reference, not copied.
5. The Anime Vector reference image must be analyzed directly, not assumed from its filename or general anime-art knowledge.
6. i18n must be present from the start, not retrofitted.
7. Real product infrastructure (Settings, About, Plugins, Help, Feedback, Community, Sponsor, User Agreement, Privacy) must exist, not be stubbed placeholders.
8. No giant `MainWindow` architecture.
9. No feature-specific conditionals spreading through the application core.
10. Design and build for continued development, not a quick prototype.

---

## 19. Final Product Goal

Viora should feel like: **a real, extensible visual creation tool whose first capability happens to be a geometric anime image conversion engine** — not "an AI-generated WPF demo containing one image filter." Every architectural, UI, documentation, plugin-system, and localization decision made while executing this prompt should visibly reinforce that Viora is intended to become a long-lived platform, and Phase 0's inspection output is the evidence that decisions were grounded in the actual project assets rather than assumptions.
