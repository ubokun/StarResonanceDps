// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つの読み込み先。部品はプラグインのフォルダから探し(<c>.deps.json</c>があればそれに従う)、
/// SDKだけは本体のものを使う(本体と同じ型にするため)。見つからない部品は既定の読み込み先(.NET・WPF)に任せる。
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    public const string SdkAssemblyName = "StarResonanceDps.PluginSdk";

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string mainAssemblyPath)
        : base(Path.GetFileNameWithoutExtension(mainAssemblyPath))
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, SdkAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
