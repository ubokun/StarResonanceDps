using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Reads a plugin-owned embedded resource set using the UI culture supplied by the host.
/// The plugin chooses its own resource base name and owns every ResX file.
/// </summary>
public sealed class PluginLocalizer : INotifyPropertyChanged, IDisposable
{
    private const string EnglishCultureName = "en-US";

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>> ResourceSets =
        new(StringComparer.Ordinal);

    private readonly IPluginLocalizationService _localization;
    private readonly Assembly _resourceAssembly;
    private readonly IReadOnlyDictionary<string, string> _resourceNamesByCulture;
    private bool _isDisposed;

    /// <param name="localization">The host-provided current-culture service.</param>
    /// <param name="resourceAssembly">The plugin assembly that embeds its resources.</param>
    /// <param name="resourceBaseName">
    /// The manifest resource base name without a culture suffix or <c>.resources</c>, for example
    /// <c>Example.Plugin.Properties.Resources</c>.
    /// </param>
    public PluginLocalizer(
        IPluginLocalizationService localization,
        Assembly resourceAssembly,
        string resourceBaseName)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _resourceAssembly = resourceAssembly ?? throw new ArgumentNullException(nameof(resourceAssembly));
        if (string.IsNullOrWhiteSpace(resourceBaseName))
        {
            throw new ArgumentException("A resource base name is required.", nameof(resourceBaseName));
        }

        _resourceNamesByCulture = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ja-JP"] = $"{resourceBaseName}.ja-JP.resources",
            ["ko-KR"] = $"{resourceBaseName}.ko-KR.resources",
            ["zh-CN"] = $"{resourceBaseName}.zh-CN.resources",
            [EnglishCultureName] = $"{resourceBaseName}.resources"
        };

        _localization.CultureChanged += Localization_CultureChanged;
    }

    /// <summary>Raised for <see cref="CurrentCulture"/> and the indexer when the UI culture changes, so bindings to the indexer update.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised on the UI thread after the host's UI culture has changed.</summary>
    public event EventHandler? CultureChanged;

    /// <summary>The host's current UI culture.</summary>
    public CultureInfo CurrentCulture => _localization.CurrentCulture;

    /// <summary>The same as <see cref="GetString"/>. Bind it from XAML with an indexer path such as <c>[WindowTitle]</c>.</summary>
    public string this[string key] => GetString(key);

    /// <summary>
    /// The text of <paramref name="key"/> in the resources of the current UI culture, then in the <c>en-US</c> resources, then the key itself.
    /// A blank key returns an empty string. Throws <see cref="MissingManifestResourceException"/> when the resources of the culture are not embedded.
    /// </summary>
    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var currentResources = GetResourceSet(CurrentCulture);
        if (currentResources.TryGetValue(key, out var localizedValue))
        {
            return localizedValue;
        }

        if (!string.Equals(CurrentCulture.Name, EnglishCultureName, StringComparison.OrdinalIgnoreCase))
        {
            var fallbackResources = GetResourceSet(CultureInfo.GetCultureInfo(EnglishCultureName));
            if (fallbackResources.TryGetValue(key, out var fallbackValue))
            {
                return fallbackValue;
            }
        }

        return key;
    }

    /// <summary>Formats the text of <paramref name="key"/> with <paramref name="arguments"/> in the current UI culture.</summary>
    public string Format(string key, params object?[] arguments)
    {
        return string.Format(CurrentCulture, GetString(key), arguments);
    }

    /// <summary>Stops following the host's culture changes. Call it when the localizer is no longer used.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _localization.CultureChanged -= Localization_CultureChanged;
        _isDisposed = true;
    }

    private void Localization_CultureChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyDictionary<string, string> GetResourceSet(CultureInfo culture)
    {
        var resourceName = _resourceNamesByCulture.TryGetValue(culture.Name, out var localizedResourceName)
            ? localizedResourceName
            : _resourceNamesByCulture[EnglishCultureName];

        var cacheKey = $"{_resourceAssembly.FullName}|{resourceName}";
        return ResourceSets.GetOrAdd(
            cacheKey,
            _ => new Lazy<IReadOnlyDictionary<string, string>>(
                () => LoadResourceSet(resourceName),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private IReadOnlyDictionary<string, string> LoadResourceSet(string resourceName)
    {
        using var stream = _resourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new MissingManifestResourceException(
                $"Embedded localization resource '{resourceName}' was not found in '{_resourceAssembly.GetName().Name}'.");
        using var reader = new ResourceReader(stream);

        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        IDictionaryEnumerator enumerator = reader.GetEnumerator();

        while (enumerator.MoveNext())
        {
            if (enumerator.Key is not string key || enumerator.Value is not string value)
            {
                throw new InvalidDataException(
                    $"Embedded localization resource '{resourceName}' contains a non-string entry.");
            }

            resources.Add(key, value);
        }

        return resources;
    }
}
