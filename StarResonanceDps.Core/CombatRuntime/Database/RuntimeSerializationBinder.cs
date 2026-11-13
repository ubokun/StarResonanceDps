// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace StarResonanceDps.Core.CombatRuntime.Database;

internal sealed class RuntimeSerializationBinder : ISerializationBinder
{
    private const string CoreNamespacePrefix = "StarResonanceDps.Core.";
    private static readonly Assembly RuntimeAssembly = typeof(Utils).Assembly;
    private static readonly string RuntimeNamespace = typeof(Utils).Namespace!;

    public Type BindToType(string? assemblyName, string typeName)
    {
        var resolvedType = ResolveDirectType(assemblyName, typeName);
        if (resolvedType is not null)
        {
            return resolvedType;
        }

        var remappedType = ResolveRenamedRuntimeType(typeName);
        if (remappedType is not null)
        {
            return remappedType;
        }

        throw new JsonSerializationException($"Unable to resolve persisted type '{typeName}'.");
    }

    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = serializedType.Assembly.GetName().Name;
        typeName = serializedType.FullName;
    }

    private static Type? ResolveDirectType(string? assemblyName, string typeName)
    {
        return string.IsNullOrWhiteSpace(assemblyName)
            ? Type.GetType(typeName, throwOnError: false)
            : Type.GetType($"{typeName}, {assemblyName}", throwOnError: false);
    }

    private static Type? ResolveRenamedRuntimeType(string serializedTypeName)
    {
        if (!serializedTypeName.StartsWith(CoreNamespacePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var relativeTypeName = serializedTypeName[CoreNamespacePrefix.Length..];
        var namespaceSeparator = relativeTypeName.IndexOf('.');
        if (namespaceSeparator < 0 || namespaceSeparator == relativeTypeName.Length - 1)
        {
            return null;
        }

        var typeName = relativeTypeName[(namespaceSeparator + 1)..];
        if (string.Equals(typeName, "CombatStats2", StringComparison.Ordinal))
        {
            typeName = "CombatStats";
        }

        return RuntimeAssembly.GetType($"{RuntimeNamespace}.{typeName}", throwOnError: false);
    }
}
