using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインの入口の DLL から、プラグインを動かすのに要るファイルを決める。
/// 部品は、DLL が参照するアセンブリをたどったもの(<c>.deps.json</c> があればそれに書かれたネイティブの DLL・言語ごとの資源の DLL も)で、
/// 読み込み先(<see cref="PluginLoadContext"/>)と同じ探し方(<see cref="AssemblyDependencyResolver"/>)で入口のフォルダから探す。
/// フォルダに無く、SDK でも本体・.NET・WPF の持つアセンブリでもなければ足りない。
/// </summary>
internal static class PluginDependencyScanner
{
    private static readonly Lazy<HashSet<string>> HostAssemblyNames = new(ReadHostAssemblyNames);

    public static PluginDependencyScan Scan(string mainAssemblyPath)
    {
        var mainPath = Path.GetFullPath(mainAssemblyPath);
        var folder = Path.GetDirectoryName(mainPath)!;
        var resolver = new AssemblyDependencyResolver(mainPath);
        var files = new List<string> { mainPath };
        var managedAssemblies = new List<string> { mainPath };
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mainPath };
        var pending = new Queue<string>();
        pending.Enqueue(mainPath);

        void AddFile(string path, bool isManaged)
        {
            if (!seenFiles.Add(path))
            {
                return;
            }

            files.Add(path);
            if (isManaged)
            {
                managedAssemblies.Add(path);
                pending.Enqueue(path);
            }
        }

        string? FindInFolder(string? resolvedPath)
        {
            return resolvedPath is not null && File.Exists(resolvedPath) && IsInside(folder, resolvedPath)
                ? Path.GetFullPath(resolvedPath)
                : null;
        }

        var depsPath = Path.ChangeExtension(mainPath, ".deps.json");
        if (File.Exists(depsPath))
        {
            files.Add(depsPath);
            foreach (var asset in ReadDepsAssets(depsPath))
            {
                var name = Path.GetFileNameWithoutExtension(asset.RelativePath);
                switch (asset.Kind)
                {
                    case DepsAssetKind.Runtime when !IsSdk(name):
                        if (FindInFolder(resolver.ResolveAssemblyToPath(new AssemblyName(name))) is { } runtimePath)
                        {
                            AddFile(runtimePath, isManaged: true);
                        }
                        else if (!asset.IsRuntimeSpecific && !HostAssemblyNames.Value.Contains(name))
                        {
                            missing.Add(Path.GetFileName(asset.RelativePath));
                        }

                        break;
                    case DepsAssetKind.Native:
                        if (FindInFolder(resolver.ResolveUnmanagedDllToPath(name)) is { } nativePath)
                        {
                            AddFile(nativePath, isManaged: false);
                        }
                        else if (!asset.IsRuntimeSpecific)
                        {
                            missing.Add(Path.GetFileName(asset.RelativePath));
                        }

                        break;
                    case DepsAssetKind.Resource:
                        if (FindInFolder(resolver.ResolveAssemblyToPath(new AssemblyName(name) { CultureName = asset.Locale })) is { } resourcePath)
                        {
                            AddFile(resourcePath, isManaged: false);
                        }
                        else
                        {
                            missing.Add($"{asset.Locale}{Path.DirectorySeparatorChar}{Path.GetFileName(asset.RelativePath)}");
                        }

                        break;
                }
            }
        }

        var seenReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.TryDequeue(out var assemblyPath))
        {
            foreach (var reference in ReadAssemblyReferences(assemblyPath))
            {
                if (reference.Name is not { } name || !seenReferences.Add(name) || IsSdk(name))
                {
                    continue;
                }

                if (FindInFolder(resolver.ResolveAssemblyToPath(reference)) is { } referencePath)
                {
                    AddFile(referencePath, isManaged: true);
                }
                else if (!HostAssemblyNames.Value.Contains(name))
                {
                    missing.Add(name + ".dll");
                }
            }
        }

        return new PluginDependencyScan(folder, files, managedAssemblies, missing.ToArray());
    }

    /// <summary>本体の読み込み先が持つアセンブリ(.NET・WPF・本体と本体の部品)の名前か。</summary>
    public static bool IsHostAssemblyName(string assemblyName)
    {
        return HostAssemblyNames.Value.Contains(assemblyName);
    }

    private static bool IsSdk(string assemblyName)
    {
        return string.Equals(assemblyName, PluginLoadContext.SdkAssemblyName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, Path.GetFullPath(path));
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static IReadOnlyList<AssemblyName> ReadAssemblyReferences(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetAssemblyReference(handle).GetAssemblyName())
            .ToArray();
    }

    /// <summary>.NET・WPF・本体と本体の部品のアセンブリのパス。</summary>
    public static IReadOnlyList<string> GetHostAssemblyPaths()
    {
        return AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [];
    }

    /// <summary>本体の読み込み先が持つアセンブリ(.NET・WPF・本体の部品)の名前。</summary>
    private static HashSet<string> ReadHostAssemblyNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in GetHostAssemblyPaths())
        {
            names.Add(Path.GetFileNameWithoutExtension(path));
        }

        foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
        {
            if (assembly.GetName().Name is { } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static IEnumerable<DepsAsset> ReadDepsAssets(string depsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(depsPath));
        var root = document.RootElement;
        var targetName = root.TryGetProperty("runtimeTarget", out var runtimeTarget)
            && runtimeTarget.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;
        if (targetName is null
            || !root.TryGetProperty("targets", out var targets)
            || !targets.TryGetProperty(targetName, out var libraries))
        {
            throw new InvalidDataException($"The dependency file has no target '{targetName}'.");
        }

        var assets = new List<DepsAsset>();
        foreach (var library in libraries.EnumerateObject())
        {
            AddAssets(library.Value, "runtime", DepsAssetKind.Runtime, assets);
            AddAssets(library.Value, "native", DepsAssetKind.Native, assets);
            if (library.Value.TryGetProperty("resources", out var resources))
            {
                foreach (var resource in resources.EnumerateObject())
                {
                    if (resource.Value.TryGetProperty("locale", out var locale) && locale.GetString() is { Length: > 0 } localeName)
                    {
                        assets.Add(new DepsAsset(DepsAssetKind.Resource, resource.Name, localeName, IsRuntimeSpecific: false));
                    }
                }
            }

            // ほかの OS・CPU 向けの部品も並ぶ。今の環境の分だけが見つかればよいので、見つからなくても足りないとしない。
            if (library.Value.TryGetProperty("runtimeTargets", out var runtimeTargets))
            {
                foreach (var runtimeAsset in runtimeTargets.EnumerateObject())
                {
                    var kind = runtimeAsset.Value.TryGetProperty("assetType", out var assetType) && assetType.GetString() == "native"
                        ? DepsAssetKind.Native
                        : DepsAssetKind.Runtime;
                    assets.Add(new DepsAsset(kind, runtimeAsset.Name, null, IsRuntimeSpecific: true));
                }
            }
        }

        return assets;
    }

    private static void AddAssets(JsonElement library, string propertyName, DepsAssetKind kind, List<DepsAsset> assets)
    {
        if (!library.TryGetProperty(propertyName, out var entries))
        {
            return;
        }

        foreach (var entry in entries.EnumerateObject())
        {
            assets.Add(new DepsAsset(kind, entry.Name, null, IsRuntimeSpecific: false));
        }
    }

    private enum DepsAssetKind
    {
        Runtime,
        Native,
        Resource,
    }

    private sealed record DepsAsset(DepsAssetKind Kind, string RelativePath, string? Locale, bool IsRuntimeSpecific);
}

/// <param name="Folder">入口の DLL のフォルダ。</param>
/// <param name="Files">プラグインを動かすのに要るファイル(入口・<c>.deps.json</c>・部品)。全部 <paramref name="Folder"/> の中。</param>
/// <param name="ManagedAssemblies">そのうち、読み込み先へ先に読む .NET のアセンブリ(入口を含む)。</param>
/// <param name="MissingFiles">足りないファイルの名前。</param>
internal sealed record PluginDependencyScan(
    string Folder,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ManagedAssemblies,
    IReadOnlyList<string> MissingFiles);
