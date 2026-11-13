// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.ModuleTool.Models;

namespace StarResonanceDps.Plugins.ModuleTool.Services;

/// <summary>
/// DLL に埋め込んだ表(<c>Data/ModuleTables.json</c>・<c>Data/ModuleNames.json</c>)と絵(<c>Assets</c>)。
/// 読めない・形が合わない・絵が無いときは例外にし、プラグインを開けない形で知らせる(値を作って埋めない)。
/// </summary>
internal sealed class ModuleCatalog
{
    private const string ResourcePrefix = "StarResonanceDps.Plugins.ModuleTool.Data.";
    private const string AssetUriFormat = "pack://application:,,,/ModuleTool;component/Assets/{0}.png";
    private const string QualityAssetFormat = "item_quality_{0}";
    private const string FallbackCultureName = "zh-CN";
    private const string AsiaLinkTableSource = "StarASIA";
    private const string DefaultLinkTableSource = "Star";

    private readonly Dictionary<int, ModuleEffectDefinition> _effects;
    private readonly Dictionary<int, ModuleDefinition> _modules;
    private readonly Dictionary<string, int[]> _linkFightValues;
    private readonly ModuleNamesFile _names;
    private readonly Dictionary<string, ImageSource> _images = new(StringComparer.Ordinal);

    public ModuleCatalog()
    {
        var tables = ReadResource<ModuleTablesFile>("ModuleTables.json");
        _names = ReadResource<ModuleNamesFile>("ModuleNames.json");

        if (tables.SlotCount <= 0)
        {
            throw new InvalidDataException("ModuleTables.json has no module slots.");
        }

        SlotCount = tables.SlotCount;
        _effects = tables.Effects.ToDictionary(
            pair => ParseId(pair.Key),
            pair => CreateEffect(ParseId(pair.Key), pair.Value));
        _modules = tables.Modules.ToDictionary(
            pair => ParseId(pair.Key),
            pair => new ModuleDefinition(ParseId(pair.Key), pair.Value.Icon, pair.Value.Quality));
        _linkFightValues = tables.LinkFightValues;
        foreach (var source in new[] { AsiaLinkTableSource, DefaultLinkTableSource })
        {
            if (!_linkFightValues.TryGetValue(source, out var values) || values.Length == 0)
            {
                throw new InvalidDataException($"ModuleTables.json has no link fight values for {source}.");
            }

            ValidateNonDecreasing(values, $"link fight values of {source}");
        }

        Effects = _effects.Values.OrderBy(effect => effect.EffectId).ToArray();
        Qualities = _modules.Values.Select(module => module.Quality).Distinct().Order().ToArray();

        // 絵は開く前に全部読む。足りなければここで止まる。
        foreach (var icon in _effects.Values.Select(effect => effect.Icon)
                     .Concat(_modules.Values.Select(module => module.Icon))
                     .Concat(Qualities.Select(GetQualityAssetName)))
        {
            _images[icon] = LoadImage(icon);
        }
    }

    public int SlotCount { get; }

    public IReadOnlyList<ModuleEffectDefinition> Effects { get; }

    /// <summary>モジュールの品質(表の値)の全部。小さい順。</summary>
    public IReadOnlyList<int> Qualities { get; }

    public bool TryGetEffect(int effectId, out ModuleEffectDefinition effect)
    {
        return _effects.TryGetValue(effectId, out effect!);
    }

    public bool TryGetModule(int configId, out ModuleDefinition module)
    {
        return _modules.TryGetValue(configId, out module!);
    }

    /// <summary>今のサーバーの「リンクの総和 → 戦闘力」。アジアサーバーだけ StarASIA の表、それ以外は Star の表。</summary>
    public IReadOnlyList<int> GetLinkFightValues(PluginGameServer server)
    {
        return _linkFightValues[server == PluginGameServer.Asia ? AsiaLinkTableSource : DefaultLinkTableSource];
    }

    public string GetEffectName(int effectId, CultureInfo culture) => GetName(_names.Effects, effectId, culture);

    public string GetModuleName(int configId, CultureInfo culture) => GetName(_names.Modules, configId, culture);

    public ImageSource GetImage(string icon) => _images[icon];

    public ImageSource GetQualityImage(int quality) => _images[GetQualityAssetName(quality)];

    private static string GetQualityAssetName(int quality)
    {
        return string.Format(CultureInfo.InvariantCulture, QualityAssetFormat, quality);
    }

    /// <summary>表示言語の名前。空なら zh-CN の名前(本体の名前と同じ決まり)。</summary>
    private static string GetName(Dictionary<string, Dictionary<string, string>> names, int id, CultureInfo culture)
    {
        if (!names.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out var byCulture))
        {
            return string.Empty;
        }

        if (byCulture.TryGetValue(culture.Name, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return byCulture.TryGetValue(FallbackCultureName, out var fallback) ? fallback : string.Empty;
    }

    private static ModuleEffectDefinition CreateEffect(int effectId, ModuleEffectFile file)
    {
        if (file.Levels.Count == 0 || string.IsNullOrWhiteSpace(file.Icon))
        {
            throw new InvalidDataException($"Module effect {effectId} has no levels or icon.");
        }

        var levels = file.Levels.Select(level => new ModuleEffectLevel(level.Links, level.FightValue)).ToArray();
        ValidateNonDecreasing(levels.Select(level => level.Links).ToArray(), $"links of module effect {effectId}");
        ValidateNonDecreasing(levels.Select(level => level.FightValue).ToArray(), $"fight values of module effect {effectId}");
        return new ModuleEffectDefinition(effectId, file.Icon, levels);
    }

    /// <summary>探索の上限は「リンクが増えれば点数は減らない」に頼るので、表がそうでなければ止める。</summary>
    private static void ValidateNonDecreasing(IReadOnlyList<int> values, string label)
    {
        for (var index = 1; index < values.Count; index++)
        {
            if (values[index] < values[index - 1])
            {
                throw new InvalidDataException($"The {label} decrease at index {index}.");
            }
        }
    }

    private static int ParseId(string key)
    {
        return int.Parse(key, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static T ReadResource<T>(string fileName)
    {
        using var stream = typeof(ModuleCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidDataException($"The embedded resource {fileName} is missing.");
        return JsonSerializer.Deserialize<T>(stream)
            ?? throw new InvalidDataException($"The embedded resource {fileName} is empty.");
    }

    private static ImageSource LoadImage(string icon)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(string.Format(CultureInfo.InvariantCulture, AssetUriFormat, icon), UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
