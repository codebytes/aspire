// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;

namespace Aspire.Cli.Import;

/// <summary>
/// Dispatches discovered resources to provider-specific mappers and produces import-ready resources.
/// </summary>
internal sealed class ResourceMappingService(IEnumerable<IResourceMapper> mappers)
{
    private readonly Dictionary<ResourceProvider, IResourceMapper> _mappersByProvider = mappers.ToDictionary(m => m.Provider);

    /// <summary>
    /// Attempts to map a single discovered resource to an imported resource.
    /// </summary>
    /// <param name="resource">The discovered resource.</param>
    /// <param name="result">The imported resource, or <c>null</c> if mapping failed.</param>
    /// <returns><c>true</c> if the resource was mapped; otherwise, <c>false</c>.</returns>
    public bool TryMap(DiscoveredResource resource, out ImportedResource? result)
    {
        result = null;

        if (!_mappersByProvider.TryGetValue(resource.Provider, out var mapper))
        {
            return false;
        }

        if (!mapper.TryMap(resource, out var mapping) || mapping is null)
        {
            return false;
        }

        result = new ImportedResource(
            Name: SanitizeName(resource.Name),
            SourceResourceName: resource.Name,
            SourceType: resource.SourceType,
            Kind: resource.Kind,
            Category: mapping.Category,
            AspireBuilderMethod: mapping.AspireBuilderMethod,
            NuGetPackage: mapping.NuGetPackage,
            FriendlyName: mapping.FriendlyName,
            SupportLevel: mapping.SupportLevel);

        return true;
    }

    /// <summary>
    /// Maps all discovered resources, returning unsupported placeholders for unmapped types.
    /// </summary>
    /// <param name="resources">The list of discovered resources.</param>
    /// <returns>A list of imported resources for all inputs.</returns>
    public List<ImportedResource> MapAll(IReadOnlyList<DiscoveredResource> resources)
    {
        var results = new List<ImportedResource>(resources.Count);

        foreach (var resource in resources)
        {
            if (TryMap(resource, out var mapped) && mapped is not null)
            {
                results.Add(mapped);
            }
            else
            {
                results.Add(new ImportedResource(
                    Name: SanitizeName(resource.Name),
                    SourceResourceName: resource.Name,
                    SourceType: resource.SourceType,
                    Kind: resource.Kind,
                    Category: ResourceCategory.Other,
                    AspireBuilderMethod: null,
                    NuGetPackage: null,
                    FriendlyName: null,
                    SupportLevel: ImportSupportLevel.Unsupported));
            }
        }

        return results;
    }

    /// <summary>
    /// Converts a source resource name to a valid camelCase C# identifier.
    /// </summary>
    internal static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "resource";
        }

        var sb = new StringBuilder(name.Length);
        var capitalizeNext = false;

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (c is '-' or '_')
            {
                capitalizeNext = true;
                continue;
            }

            if (!char.IsLetterOrDigit(c))
            {
                capitalizeNext = true;
                continue;
            }

            if (sb.Length == 0)
            {
                if (char.IsDigit(c))
                {
                    sb.Append('r');
                }

                sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
            }
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpper(c, CultureInfo.InvariantCulture));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }

        var result = sb.Length == 0 ? "resource" : sb.ToString();
        return IsCSharpKeyword(result) ? $"@{result}" : result;
    }

    private static bool IsCSharpKeyword(string value)
    {
        return value switch
        {
            "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or
            "char" or "checked" or "class" or "const" or "continue" or "decimal" or "default" or
            "delegate" or "do" or "double" or "else" or "enum" or "event" or "explicit" or "extern" or
            "false" or "finally" or "fixed" or "float" or "for" or "foreach" or "goto" or "if" or
            "implicit" or "in" or "int" or "interface" or "internal" or "is" or "lock" or "long" or
            "namespace" or "new" or "null" or "object" or "operator" or "out" or "override" or
            "params" or "private" or "protected" or "public" or "readonly" or "ref" or "return" or
            "sbyte" or "sealed" or "short" or "sizeof" or "stackalloc" or "static" or "string" or
            "struct" or "switch" or "this" or "throw" or "true" or "try" or "typeof" or "uint" or
            "ulong" or "unchecked" or "unsafe" or "ushort" or "using" or "virtual" or "void" or
            "volatile" or "while" => true,
            _ => false
        };
    }
}
