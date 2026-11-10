using Microsoft.CodeAnalysis;

namespace StarResonanceDps.PluginSdk.Analyzers;

/// <summary>
/// プラグインの検査。属性の検査の中身は本体が読み込むときの確かめ(<c>PluginManager.CreateRegistration</c>)と同じにする
/// (ビルドで通ったものが本体で弾かれないように)。公開した検査の ID を変えない・消さない(AnalyzerReleases の表と組)。
/// 属性の検査はコンパイルの終わりに1回だけ走るので <see cref="WellKnownDiagnosticTags.CompilationEnd"/> を付ける。定義の検査(<see cref="PluginDefinitionAnalyzer"/>)には付けない。
/// </summary>
internal static class PluginDiagnostics
{
    public const string Category = "StarResonanceDps.Plugin";

    public static readonly DiagnosticDescriptor MissingRegistration = new(
        "SRDPS001",
        "Plugin registration attribute is missing",
        "This assembly defines a plugin, but it has no [assembly: PluginRegistration(...)] attribute",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor ApiVersionMismatch = new(
        "SRDPS003",
        "Plugin API version does not match the SDK",
        "The plugin API version is {0}, but the referenced SDK is version {1}; pass PluginSdkVersion.Current",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor InvalidEntryType = new(
        "SRDPS004",
        "Plugin entry type is invalid",
        "The plugin entry type '{0}' {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor MissingDisplayName = new(
        "SRDPS005",
        "Plugin display name is missing",
        "Add at least one [assembly: PluginDisplayName(culture, name)] attribute with a culture name and a display name",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor DuplicateCulture = new(
        "SRDPS006",
        "Duplicate culture in plugin attributes",
        "More than one {0} attribute is declared for the culture '{1}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor MissingAuthor = new(
        "SRDPS007",
        "Plugin author is missing",
        "Add an [assembly: PluginAuthor(author)] attribute with a non-empty author",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor MissingGenre = new(
        "SRDPS008",
        "Plugin genre is missing",
        "The plugin opens a window (IPluginWindowProvider), so add at least one [assembly: PluginGenre(culture, genre)] attribute with a culture name and a genre",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor UnusedGenre = new(
        "SRDPS009",
        "Plugin genre is not used",
        "The plugin does not open a window (IPluginWindowProvider), so PluginGenre is not shown in the plugin list",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor EmptyLocalizedText = new(
        "SRDPS010",
        "Localized plugin text is empty",
        "This {0} attribute has an empty culture name or text, so it is ignored",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor EmptyIconPath = new(
        "SRDPS011",
        "Plugin icon path is empty",
        "The PluginIcon path is empty, so the app icon is shown",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor InvalidVersion = new(
        "SRDPS012",
        "Plugin version is missing or invalid",
        "Add an [assembly: PluginVersion(version)] attribute with three numbers separated by dots, for example 1.0.0",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor EmptyWidgetTargets = new(
        "SRDPS013",
        "Multi-window widget never has targets",
        "GetWidgetTargets always returns an empty list, so the widget's windows always stay empty; return at least one target",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyRequiredText = new(
        "SRDPS014",
        "Required plugin text is empty",
        "{0} has no text; set at least one culture with a non-blank text",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
