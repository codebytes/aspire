// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Discovers live Azure resources using Azure Resource Manager.
/// </summary>
internal interface IAzureResourceDiscoveryService
{
    Task<IReadOnlyList<(string Id, string Name)>> GetSubscriptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<(string Name, string Location)>> GetResourceGroupsAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DiscoveredResource>> DiscoverResourcesAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken);
}
