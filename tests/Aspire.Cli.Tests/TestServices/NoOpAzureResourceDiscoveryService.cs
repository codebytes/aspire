// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Import;

namespace Aspire.Cli.Tests.TestServices;

internal sealed class NoOpAzureResourceDiscoveryService : IAzureResourceDiscoveryService
{
    public Task<IReadOnlyList<(string Id, string Name)>> GetSubscriptionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(string Id, string Name)>>([]);

    public Task<IReadOnlyList<(string Name, string Location)>> GetResourceGroupsAsync(string subscriptionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(string Name, string Location)>>([]);

    public Task<IReadOnlyList<DiscoveredResource>> DiscoverResourcesAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiscoveredResource>>([]);
}
