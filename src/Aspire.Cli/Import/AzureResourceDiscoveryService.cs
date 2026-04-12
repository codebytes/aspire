// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Aspire.Cli.Import;

/// <summary>
/// Discovers live Azure resources using the Azure Resource Manager SDK.
/// </summary>
internal sealed class AzureResourceDiscoveryService(ILogger<AzureResourceDiscoveryService> logger) : IAzureResourceDiscoveryService
{
    private readonly Lazy<ArmClient> _armClient = new(() => new ArmClient(new DefaultAzureCredential()));

    public async Task<IReadOnlyList<(string Id, string Name)>> GetSubscriptionsAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Enumerating Azure subscriptions.");

        var results = new List<(string Id, string Name)>();

        await foreach (var subscription in _armClient.Value.GetSubscriptions().GetAllAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add((subscription.Data.SubscriptionId, subscription.Data.DisplayName));
        }

        logger.LogDebug("Found {Count} subscriptions.", results.Count);
        return results;
    }

    public async Task<IReadOnlyList<(string Name, string Location)>> GetResourceGroupsAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        logger.LogDebug("Enumerating resource groups for subscription {SubscriptionId}.", subscriptionId);

        var subscription = await _armClient.Value.GetSubscriptions().GetAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        var results = new List<(string Name, string Location)>();

        await foreach (var rg in subscription.Value.GetResourceGroups().GetAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            results.Add((rg.Data.Name, rg.Data.Location.Name));
        }

        logger.LogDebug("Found {Count} resource groups.", results.Count);
        return results;
    }

    public async Task<IReadOnlyList<DiscoveredResource>> DiscoverResourcesAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken)
    {
        logger.LogDebug("Discovering resources in subscription {SubscriptionId}, resource group {ResourceGroup}.", subscriptionId, resourceGroupName);

        var subscription = await _armClient.Value.GetSubscriptions().GetAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        var resourceGroup = await subscription.Value.GetResourceGroups().GetAsync(resourceGroupName, cancellationToken).ConfigureAwait(false);

        var results = new List<DiscoveredResource>();

        await foreach (var resource in resourceGroup.Value.GetGenericResourcesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var data = resource.Data;

            IDictionary<string, string>? tags = data.Tags is { Count: > 0 }
                ? new Dictionary<string, string>(data.Tags)
                : null;

            results.Add(new DiscoveredResource(
                Name: data.Name,
                SourceType: data.ResourceType.ToString(),
                Kind: data.Kind,
                Location: data.Location.Name,
                SourceAddress: data.Id.ToString(),
                Provider: ResourceProvider.Azure,
                Tags: tags));
        }

        logger.LogDebug("Discovered {Count} resources.", results.Count);
        return results;
    }
}
