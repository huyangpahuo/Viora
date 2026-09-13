# Viora Plugin Development Guide

Viora's extensibility contract: everything a plugin can do is expressed through
`Viora.PluginSdk` (the only assembly you reference), registered through `IPluginContext`.

## 1. Anatomy of a plugin

```text
MyPlugin/
├── plugin.json        # manifest (metadata-only read at discovery)
├── MyPlugin.dll       # entry assembly (name matches manifest entryAssembly)
└── *.dll              # plugin-private dependencies (resolved from this folder first)
```

`plugin.json`:

```json
{
  "id": "com.author.pluginname",
  "displayName": "Plugin Name",
  "version": "1.0.0",
  "author": "Author",
  "description": "What it does.",
  "homepage": "https://example.org",
  "repository": "https://github.com/author/pluginname",
  "requiredHostVersion": ">=1.0.0 <2.0.0",
  "entryAssembly": "MyPlugin.dll",
  "typeName": "Author.PluginName.PluginClass",
  "capabilities": ["convert.preset", "localization.strings"],
  "dependencies": []
}
```

## 2. The entry class

```csharp
public sealed class MyPlugin : VioraPluginBase   // from Viora.PluginSdk
{
    public MyPlugin() : base(new PluginMetadata(
        Id: "com.author.pluginname",
        DisplayName: "Plugin Name",
        Version: new Version(1, 0, 0),
        Author: "Author",
        Description: "What it does.",
        Homepage: null, Repository: null,
        RequiredHostVersion: VersionRange.Parse(">=1.0.0"),
        Capabilities: new[] { "convert.preset", "localization.strings" },
        Dependencies: Array.Empty<PluginDependency>())) { }

    public override Task InitializeAsync(IPluginContext context, CancellationToken ct)
    {
        context.RegisterPreset(new MyPreset());
        context.RegisterStrings(new Dictionary<string, string>
        {
            ["en::Preset.My.Name"] = "My Style",
            ["zh-Hans::Preset.My.Name"] = "我的风格",
        });
        return Task.CompletedTask;
    }
}
```

## 3. Capabilities

| Capability | Grants |
|---|---|
| `convert.preset` | `RegisterPreset(IStylePreset)` |
| `export.format` | `RegisterExporter(IImageExporter)` |
| `ui.panel` | `RegisterSettingsPage(ISettingsPageDescriptor)` |
| `localization.strings` | `RegisterStrings(...)` |

The host silently ignores registrations outside the declared capabilities (logged as a warning)
— this is the seam for a future permission system.

## 4. Writing a stylization preset

A preset = metadata + a `BuildPipeline(parameters)` that returns an ordered list of
`IImageProcessingStage`. Stages receive an `ImageProcessingContext` (source buffer, working
buffer, parameters) and must:

- honor `CancellationToken` inside pixel loops (checked at least per row),
- report `IProgress<StageProgress>` when work is long,
- contain **no WPF/framework UI types** — operate on `IImageBuffer` (BGRA bytes) only,
- be deterministic given the same inputs and parameters (tests rely on this).

Reference implementation: `samples/SamplePlugin` (Duotone preset, ~30 lines of stage code).

## 5. Lifecycle & packaging

- **Install** — Plugins page → *Install from file…* → pick a `.vplugin` (a zip whose root
  contains `plugin.json`). The host validates the manifest, extracts to `plugins/<id>/`.
- **Enable/Disable** — toggle on the plugin card; immediate, persisted, no restart.
- **Uninstall** — trash icon → confirmation → shutdown, unload, folder deleted.
- **Compatibility** — if `requiredHostVersion` doesn't match the running host, the plugin shows
  as *Incompatible* with a localized explanation and never initializes.
- **Failure containment** — a throwing plugin is marked *Failed*; Viora keeps running; the log
  entry is available from the Plugins page.

Build a package with the sample script as a template: `tools/build-sample-plugin.ps1`.

## 6. Isolation model notes

- Each plugin loads into its own **collectible `AssemblyLoadContext`**; `Viora.Core` and
  `Viora.PluginSdk` unify with the host's copies, everything else resolves from your folder.
- Unload happens on disable/uninstall; if your plugin leaks live types (static events, running
  threads), the host logs *unload deferred* and keeps it disabled-but-resident until restart.
  Clean shutdown (`ShutdownAsync`) avoids this.
- Plugins do **not** receive raw filesystem/registry access — only the scoped registration
  surface above. Sandboxed storage arrives with a future capability.
