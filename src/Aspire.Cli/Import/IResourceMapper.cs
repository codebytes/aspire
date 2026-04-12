// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Maps discovered infrastructure resources to their Aspire equivalents for a specific provider.
/// </summary>
internal interface IResourceMapper
{
    /// <summary>
    /// Gets the cloud or platform provider this mapper handles.
    /// </summary>
    ResourceProvider Provider { get; }

    /// <summary>
    /// Attempts to map a discovered resource to its Aspire equivalent.
    /// </summary>
    /// <param name="resource">The discovered resource to map.</param>
    /// <param name="mapping">The mapped resource, or <c>null</c> if the resource type is not recognized.</param>
    /// <returns><c>true</c> if the resource type was mapped; otherwise, <c>false</c>.</returns>
    bool TryMap(DiscoveredResource resource, out MappedResource? mapping);

    /// <summary>
    /// Gets all known resource type mappings for this provider.
    /// </summary>
    /// <returns>A read-only dictionary keyed by source type.</returns>
    IReadOnlyDictionary<string, MappedResource> GetAllMappings();

    /// <summary>
    /// Generates a comment for a resource type that is not supported by Aspire import.
    /// </summary>
    /// <param name="sourceType">The source resource type identifier.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>A TODO comment string indicating the resource is unsupported.</returns>
    string GetUnsupportedResourceComment(string sourceType, string name);
}
