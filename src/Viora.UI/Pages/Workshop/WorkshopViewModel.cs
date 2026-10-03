using System.IO;
using System.Runtime.Loader;
using System.Text;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Viora.UI.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.PluginSdk;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊:在应用内编写 C# 风格插件源码,内存编译(Roslyn)后实时预览,
/// 一键打包 zip 并生成 registry.json 条目。编译基于程序旁 sdk\ 参考程序集,
/// 单文件发布同样可用;这层"源码 → 预览 → 包"的管线即为后续 AI/MCP 接入的入口。
/// </summary>
public partial class WorkshopViewModel : ObservableObject
{
    private const string AssemblyName = "WorkshopPlugin";

    private readonly IImageConversionEngine _engine;
    private readonly IImportServiceProxy _import;
    private readonly IUiAlert _alert;
    private readonly ILogger<WorkshopViewModel> _logger;
    private IImageBuffer? _sampleBuffer;
    private AssemblyLoadContext? _lastAlc;

    public WorkshopViewModel(IImageConversionEngine engine, IImportServiceProxy import,
        IUiAlert alert, ILogger<WorkshopViewModel> logger)
    {
        _engine = engine;
        _import = import;
        _alert = alert;
        _logger = logger;
        SourceCode = DefaultTemplate;
        Categories = Viora.UI.StyleCategories.AllKeys
            .Select(k => new CategoryItem(k, Tr.Get(k)))
            .ToList();
        CreateSyntheticSample();
    }

    public IReadOnlyList<CategoryItem> Categories { get; }

    public sealed record CategoryItem(string Key, string Display);

    [ObservableProperty]
    private string _sourceCode;

    [ObservableProperty]
    private string _pluginId = "builtin.viora.my-style";

    [ObservableProperty]
    private string _displayName = "My Style";

    [ObservableProperty]
    private string _version = "1.0.0";

    [ObservableProperty]
    private string _author = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _categoryKey = "Style.Cat.Experimental";

    [ObservableProperty]
    private string _outputLog = "";

    [ObservableProperty]
    private string _registryText = "";

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private string _sampleInfoText = Tr.Get("Workshop.Sample.BuiltIn");

    [ObservableProperty]
    private bool _isBusy;

    public string ApiHelpText => ApiHelp;

    // ---------- 编译 ----------

    private List<MetadataReference> BuildReferences()
    {
        // sdk\ 目录 = 发布时随包附带的参考程序集(netstandard + System.* + Core/PluginSdk/Logging)
        // 缺目录时回退到当前运行环境(开发态目录布局)。
        string sdkDir = Path.Combine(AppContext.BaseDirectory, "sdk");
        var references = new List<MetadataReference>();
        if (Directory.Exists(sdkDir))
        {
            // 精选集:netstandard + System.Runtime + 转发常用的 System.* 门面 + 契约程序集,
            // 避免门面与实现程序集混用造成的类型身份冲突
            string[] names =
            {
                "netstandard.dll", "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll",
                "System.Threading.dll", "System.Threading.Tasks.dll", "System.Threading.Tasks.Parallel.dll",
                "System.IO.dll", "System.Runtime.Extensions.dll", "System.ObjectModel.dll",
                "Viora.Core.dll", "Viora.PluginSdk.dll", "Microsoft.Extensions.Logging.Abstractions.dll",
            };
            foreach (var name in names)
            {
                string path = Path.Combine(sdkDir, name);
                if (File.Exists(path)) references.Add(MetadataReference.CreateFromFile(path));
            }
        }
        else
        {
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            foreach (var name in new[] { "Viora.Core.dll", "Viora.PluginSdk.dll" })
            {
                string path = Path.Combine(AppContext.BaseDirectory, name);
                if (File.Exists(path)) references.Add(MetadataReference.CreateFromFile(path));
            }
        }
        return references;
    }

    private async Task<(byte[]? Pe, string Diagnostics)> CompileAsync(string source, CancellationToken ct)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            AssemblyName,
            new[] { tree },
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithWarningLevel(1));

        await using var ms = new MemoryStream();
        var result = compilation.Emit(ms, cancellationToken: ct);
        var log = new StringBuilder();
        foreach (var d in result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Take(20))
            log.AppendLine(d.ToString());
        if (!result.Success)
        {
            foreach (var d in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(30))
                log.AppendLine(d.ToString());
            return (null, log.ToString());
        }
        ms.Position = 0;
        return (ms.ToArray(), log.ToString());
    }

    /// <summary>编译并加载工坊程序集,返回实现 IStylePreset 的实例。</summary>
    private async Task<IStylePreset?> CompilePresetAsync(CancellationToken ct)
    {
        Log("—— 编译 ——");
        var (pe, diagnostics) = await CompileAsync(SourceCode, ct);
        Log(string.IsNullOrWhiteSpace(diagnostics) ? "(无警告)" : diagnostics.TrimEnd());
        if (pe is null)
        {
            Log(Tr.Get("Workshop.CompileFailed"));
            return null;
        }

        try { _lastAlc?.Unload(); } catch { /* 上一份仍在使用时放弃卸载 */ }

        var alc = new AssemblyLoadContext($"workshop-{Guid.NewGuid():N}", isCollectible: true);
        using var ms = new MemoryStream(pe);
        var assembly = alc.LoadFromStream(ms);
        _lastAlc = alc;

        var presetType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IStylePreset).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
        if (presetType is null)
        {
            Log(Tr.Get("Workshop.NoPreset"));
            return null;
        }
        var preset = (IStylePreset)(Activator.CreateInstance(presetType)
            ?? throw new InvalidOperationException("Preset constructor returned null."));
        Log($"Preset: {preset.Id} ({preset.Parameters.Count} 个参数)");
        return preset;
    }

    // ---------- 命令 ----------

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task RunPreviewAsync(CancellationToken ct)
    {
        if (IsBusy || _sampleBuffer is null) return;
        IsBusy = true;
        try
        {
            var preset = await CompilePresetAsync(ct);
            if (preset is null) return;

            Log("—— 渲染 ——");
            var parameters = preset.Parameters.ToDictionary(p => p.Key, p => p.DefaultValue);
            var result = await _engine.ExecuteAsync(
                preset.BuildPipeline(parameters), _sampleBuffer.Clone(), parameters,
                previewQuality: true, progress: new Progress<PipelineProgress>(p =>
                {
                    if (p.CurrentStage is { } s)
                        Log($"  {s.StageName} {s.StageFraction:P0}");
                }), cancellationToken: ct);
            PreviewImage = _import.ToImageSource(result.Result);
            Log($"完成({result.Duration.TotalMilliseconds:F0} ms)");
        }
        catch (Exception ex)
        {
            Log($"运行失败: {ex.Message}");
            _logger.LogError(ex, "Workshop preview failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task PackZipAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(PluginId) || PluginId.Contains("..") || PluginId.Contains('/'))
        {
            _alert.Warn(Tr.Get("Workshop.Pack"), Tr.Get("Workshop.BadId"));
            return;
        }
        IsBusy = true;
        try
        {
            var (pe, _) = await CompileAsync(SourceCode, ct);
            if (pe is null)
            {
                Log(Tr.Get("Workshop.CompileFailed"));
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Plugin package (*.zip)|*.zip",
                FileName = PluginId + ".zip",
            };
            if (dialog.ShowDialog() != true) return;

            var manifest = new Dictionary<string, object>
            {
                ["id"] = PluginId,
                ["displayName"] = DisplayName,
                ["version"] = Version,
                ["author"] = string.IsNullOrWhiteSpace(Author) ? "unknown" : Author,
                ["description"] = Description,
                ["requiredHostVersion"] = "*",
                ["entryAssembly"] = AssemblyName + ".dll",
                ["typeName"] = "",
                ["capabilities"] = new[] { "convert.preset", "localization.strings" },
                ["dependencies"] = Array.Empty<object>(),
            };
            // typeName 从编译结果里解析:工坊源码必须包含一个实现 IVioraPlugin 的入口类(默认模板已带)
            var alc = new System.Runtime.Loader.AssemblyLoadContext("workshop-pack", isCollectible: true);
            try
            {
                using var ms = new MemoryStream(pe);
                var assembly = alc.LoadFromStream(ms);
                var entryType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IVioraPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
                if (entryType is null)
                {
                    _alert.Warn(Tr.Get("Workshop.Pack"), Tr.Get("Workshop.NeedEntry"));
                    Log(Tr.Get("Workshop.NeedEntry"));
                    return;
                }
                manifest["typeName"] = entryType.FullName ?? entryType.Name;
            }
            finally
            {
                try { alc.Unload(); } catch { }
            }

            using (var fs = new FileStream(dialog.FileName, FileMode.Create))
            using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
            {
                var manifestEntry = zip.CreateEntry("plugin.json");
                using (var w = new StreamWriter(manifestEntry.Open()))
                    w.Write(System.Text.Json.JsonSerializer.Serialize(manifest,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                var dllEntry = zip.CreateEntry(AssemblyName + ".dll");
                using (var w = dllEntry.Open())
                    w.Write(pe, 0, pe.Length);
            }

            Log($"已导出: {dialog.FileName}");
            _alert.Info(string.Format(Tr.Get("Workshop.PackDone"), dialog.FileName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workshop pack failed");
            Log($"打包失败: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void GenerateRegistry()
    {
        string presetId = PluginId.StartsWith("builtin.viora.", StringComparison.Ordinal)
            ? "builtin." + PluginId["builtin.viora.".Length..]
            : PluginId;
        var entry = new
        {
            pluginId = PluginId,
            presetId,
            name = new { zh = DisplayName, en = DisplayName },
            author = string.IsNullOrWhiteSpace(Author) ? "unknown" : Author,
            category = CategoryKey,
            version = Version,
            package = $"packages/{PluginId}.zip",
            repository = "",
            tags = Array.Empty<string>(),
            description = new { zh = Description, en = Description },
        };
        RegistryText = System.Text.Json.JsonSerializer.Serialize(entry,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        try
        {
            System.Windows.Clipboard.SetText(RegistryText);
            Log("registry 条目已生成并复制到剪贴板");
        }
        catch
        {
            Log("registry 条目已生成(剪贴板不可用,请在文本框复制)");
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task PickSampleAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp",
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _sampleBuffer = await _import.LoadFromFileAsync(dialog.FileName,
                1024); // 预览上限,与风格化页一致
            SampleInfoText = System.IO.Path.GetFileName(dialog.FileName);
            Log($"样张: {SampleInfoText}");
        }
        catch (Exception ex)
        {
            _alert.Warn(Tr.Get("Workshop.PickSample"), ex.Message);
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ResetSample()
    {
        CreateSyntheticSample();
        SampleInfoText = Tr.Get("Workshop.Sample.BuiltIn");
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void CopyRegistry()
    {
        if (string.IsNullOrEmpty(RegistryText)) return;
        try { System.Windows.Clipboard.SetText(RegistryText); } catch { }
    }

    // ---------- 样张 / 日志 ----------

    private void CreateSyntheticSample()
    {
        // 内置合成样张:天空渐变 + 山形三角 + 太阳圆,足以判断风格效果
        int w = 512, h = 320;
        var buffer = new RgbaImageBuffer(w, h);
        var px = buffer.Pixels;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * buffer.Stride + x * 4;
                double t = (double)y / h;
                px[i] = Clamp((int)(150 + t * 90));
                px[i + 1] = Clamp((int)(170 + t * 50));
                px[i + 2] = Clamp((int)(235 - t * 60));
                px[i + 3] = 255;
            }
        }
        // 太阳
        FillCircle(px, buffer.Stride, w, h, w * 0.72, h * 0.28, 34, (60, 150, 250));
        // 双层山
        FillTriangle(px, buffer.Stride, w, h, w * 0.10, h, w * 0.42, h * 0.34, w * 0.74, h, (52, 96, 88));
        FillTriangle(px, buffer.Stride, w, h, w * 0.38, h, w * 0.72, h * 0.5, w * 1.05, h, (36, 70, 66));
        _sampleBuffer = buffer;
        PreviewImage = _import.ToImageSource(buffer);
    }

    private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

    private static void FillCircle(byte[] px, int stride, int w, int h,
        double cx, double cy, double r, (byte B, byte G, byte R) color)
    {
        for (int y = Math.Max(0, (int)(cy - r)); y < Math.Min(h, cy + r); y++)
            for (int x = Math.Max(0, (int)(cx - r)); x < Math.Min(w, cx + r); x++)
            {
                double dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r * r) continue;
                int i = y * stride + x * 4;
                px[i] = color.B; px[i + 1] = color.G; px[i + 2] = color.R;
            }
    }

    private static void FillTriangle(byte[] px, int stride, int w, int h,
        double x0, double y0, double x1, double y1, double x2, double y2, (byte B, byte G, byte R) color)
    {
        int xa = Math.Max(0, (int)Math.Min(x0, Math.Min(x1, x2)));
        int xb = Math.Min(w - 1, (int)Math.Max(x0, Math.Max(x1, x2)));
        int ya = Math.Max(0, (int)Math.Min(y0, Math.Min(y1, y2)));
        int yb = Math.Min(h - 1, (int)Math.Max(y0, Math.Max(y1, y2)));
        double area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
        if (Math.Abs(area) < 1) return;
        for (int y = ya; y <= yb; y++)
        {
            for (int x = xa; x <= xb; x++)
            {
                double w0 = ((x1 - x0) * (y - y0) - (x - x0) * (y1 - y0)) / area;
                double w1 = ((x - x0) * (y2 - y0) - (x2 - x0) * (y - y0)) / area;
                double w2 = 1 - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                int i = y * stride + x * 4;
                px[i] = color.B; px[i + 1] = color.G; px[i + 2] = color.R;
            }
        }
    }

    private void Log(string line)
    {
        OutputLog += line + Environment.NewLine;
    }

    // ---------- 模板与 API 速查 ----------

    public const string DefaultTemplate =
"""
// Viora 风格插件 —— 一个文件即一个完整插件(打包时编译为 DLL)
// 必须包含:1) 一个实现 IStylePreset 的风格类;2) 一个实现 IVioraPlugin 的入口类。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.PluginSdk;

namespace WorkshopPlugin;

public sealed class MyStylePreset : IStylePreset
{
    public string Id => "builtin.viora.my-style";
    public string DisplayNameKey => "Preset.MyStyle.Name";   // 建议在 InitializeAsync 里自携带文案
    public string DescriptionKey => "Preset.MyStyle.Description";
    public string? IconGlyph => null;

    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        // 键名、文案键、默认值、最小、最大、步长
        new PresetParameter("intensity", "Param.Saturation", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("levels",    "Param.Colors",     6,   2,   16,  1),
    };

    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters) =>
        new IImageProcessingStage[] { new SepiaStage(), new PosterizeStage() };
}

// ---- 阶段:逐个处理 context.Working!.Pixels(BGRA,索引 = y*Stride + x*4) ----
public abstract class StageBase : IImageProcessingStage
{
    public abstract string Name { get; }
    public abstract Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct);

    protected static int Int(IReadOnlyDictionary<string, object> p, string k, int fb) =>
        p.TryGetValue(k, out var v) && v is IConvertible c ? Convert.ToInt32(c) : fb;
    protected static double Dbl(IReadOnlyDictionary<string, object> p, string k, double fb) =>
        p.TryGetValue(k, out var v) && v is IConvertible c ? Convert.ToDouble(c) : fb;
    protected static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
}

public sealed class SepiaStage : StageBase
{
    public override string Name => "Sepia";
    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        var px = src.Pixels;
        Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < src.Width; x++)
            {
                int i = y * src.Stride + x * 4;
                int luma = (px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000;
                px[i]     = Clamp((int)(luma * 0.82));
                px[i + 1] = Clamp((int)(luma * 0.70));
                px[i + 2] = Clamp((int)(luma * 0.52));
            }
        });
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}

public sealed class PosterizeStage : StageBase
{
    public override string Name => "Posterize";
    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        int levels = Int(context.Parameters, "levels", 6);
        var src = context.Working!;
        var px = src.Pixels;
        int q = 256 / Math.Max(2, levels);
        Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < src.Width; x++)
            {
                int i = y * src.Stride + x * 4;
                px[i]     = Clamp(px[i]     / q * q + q / 2);
                px[i + 1] = Clamp(px[i + 1] / q * q + q / 2);
                px[i + 2] = Clamp(px[i + 2] / q * q + q / 2);
            }
        });
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}

// ---- 入口类:宿主通过 plugin.json 的 typeName 创建它 ----
public sealed class MyStylePlugin : VioraPluginBase
{
    private static readonly PluginMetadata Meta = new(
        Id: "builtin.viora.my-style",
        DisplayName: "My Style",
        Version: new Version(1, 0, 0),
        Author: "you",
        Description: "My first Viora style.",
        Homepage: null, Repository: null,
        RequiredHostVersion: VersionRange.Any,
        Capabilities: new[] { "convert.preset", "localization.strings" },
        Dependencies: Array.Empty<PluginDependency>());

    public MyStylePlugin() : base(Meta) { }

    public override Task InitializeAsync(IPluginContext context, CancellationToken ct)
    {
        // 自携带界面文案:手动导入也能正确显示名称(不依赖宿主语言包)
        context.RegisterStrings(new Dictionary<string, string>
        {
            ["zh-Hans::Preset.MyStyle.Name"] = "我的风格",
            ["en::Preset.MyStyle.Name"] = "My Style",
            ["zh-Hans::Preset.MyStyle.Description"] = "我自定义的风格。",
            ["en::Preset.MyStyle.Description"] = "My own style.",
        });
        context.RegisterPreset(new MyStylePreset());
        return Task.CompletedTask;
    }
}
""";

    public const string ApiHelp = """
        【风格契约】IStylePreset:Id / DisplayNameKey / DescriptionKey / Parameters / BuildPipeline(参数表 → 阶段列表)
        【参数】PresetParameter(键, 文案键, 默认值, 最小, 最大, 步长)——文案键用内置 Param.* 可复用现成翻译
        【阶段】IImageProcessingStage.ExecuteAsync(context, progress, ct):处理 context.Working!.Pixels(BGRA,y*Stride+x*4)
        【基类】StageBase:Int/Dbl/Bool 读参数;PixelOps:Luma/Clamp/BlendPixel
        【进度】progress?.Report(new StageProgress(名称, 0, 1, 0~1));取消始终透传 ct
        【入口】VioraPluginBase:Metadata + InitializeAsync(context)(RegisterPreset / RegisterStrings)
        【文案】context.RegisterStrings({"zh-Hans::Key": "值", ...}),能力需含 localization.strings
        【红线】禁止联网/遥测;Id 发布后不可改;不要重复打包 Viora.Core/PluginSdk
""";
}
