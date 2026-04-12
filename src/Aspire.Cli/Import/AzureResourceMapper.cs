// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;

namespace Aspire.Cli.Import;

/// <summary>
/// Maps Azure ARM resource types to their Aspire builder equivalents.
/// </summary>
internal sealed class AzureResourceMapper : IResourceMapper
{
    private static readonly FrozenDictionary<string, MappedResource> s_mappings = CreateMappings();

    public ResourceProvider Provider => ResourceProvider.Azure;

    public bool TryMap(DiscoveredResource resource, out MappedResource? mapping)
    {
        return s_mappings.TryGetValue(resource.SourceType, out mapping);
    }

    public IReadOnlyDictionary<string, MappedResource> GetAllMappings() => s_mappings;

    public string GetUnsupportedResourceComment(string sourceType, string name)
    {
        return $"// TODO: Resource \"{name}\" ({sourceType}) is not supported by Aspire import.";
    }

    private static FrozenDictionary<string, MappedResource> CreateMappings()
    {
        var mappings = new Dictionary<string, MappedResource>(StringComparer.OrdinalIgnoreCase)
        {
            // Database (4)
            ["Microsoft.DocumentDB/databaseAccounts"] = new MappedResource(
                "Microsoft.DocumentDB/databaseAccounts",
                "AddAzureCosmosDB",
                "Aspire.Hosting.Azure.CosmosDB",
                ResourceCategory.Database,
                "Azure Cosmos DB",
                ImportSupportLevel.Supported),

            ["Microsoft.Sql/servers"] = new MappedResource(
                "Microsoft.Sql/servers",
                "AddAzureSqlServer",
                "Aspire.Hosting.Azure.Sql",
                ResourceCategory.Database,
                "Azure SQL Server",
                ImportSupportLevel.Supported),

            ["Microsoft.DBforPostgreSQL/flexibleServers"] = new MappedResource(
                "Microsoft.DBforPostgreSQL/flexibleServers",
                "AddAzurePostgresFlexibleServer",
                "Aspire.Hosting.Azure.PostgreSQL",
                ResourceCategory.Database,
                "Azure PostgreSQL Flexible Server",
                ImportSupportLevel.Supported),

            ["Microsoft.Kusto/clusters"] = new MappedResource(
                "Microsoft.Kusto/clusters",
                "AddKustoCluster",
                "Aspire.Hosting.Azure.Kusto",
                ResourceCategory.Database,
                "Azure Data Explorer (Kusto)",
                ImportSupportLevel.Supported),

            // Messaging (4)
            ["Microsoft.ServiceBus/namespaces"] = new MappedResource(
                "Microsoft.ServiceBus/namespaces",
                "AddAzureServiceBus",
                "Aspire.Hosting.Azure.ServiceBus",
                ResourceCategory.Messaging,
                "Azure Service Bus",
                ImportSupportLevel.Supported),

            ["Microsoft.EventHub/namespaces"] = new MappedResource(
                "Microsoft.EventHub/namespaces",
                "AddAzureEventHubs",
                "Aspire.Hosting.Azure.EventHubs",
                ResourceCategory.Messaging,
                "Azure Event Hubs",
                ImportSupportLevel.Supported),

            ["Microsoft.SignalRService/signalR"] = new MappedResource(
                "Microsoft.SignalRService/signalR",
                "AddAzureSignalR",
                "Aspire.Hosting.Azure.SignalR",
                ResourceCategory.Messaging,
                "Azure SignalR Service",
                ImportSupportLevel.Supported),

            ["Microsoft.SignalRService/webPubSub"] = new MappedResource(
                "Microsoft.SignalRService/webPubSub",
                "AddAzureWebPubSub",
                "Aspire.Hosting.Azure.WebPubSub",
                ResourceCategory.Messaging,
                "Azure Web PubSub",
                ImportSupportLevel.Supported),

            // Storage (1)
            ["Microsoft.Storage/storageAccounts"] = new MappedResource(
                "Microsoft.Storage/storageAccounts",
                "AddAzureStorage",
                "Aspire.Hosting.Azure.Storage",
                ResourceCategory.Storage,
                "Azure Storage",
                ImportSupportLevel.Supported),

            // Cache (2)
            ["Microsoft.Cache/redis"] = new MappedResource(
                "Microsoft.Cache/redis",
                "AddAzureRedis",
                "Aspire.Hosting.Azure.Redis",
                ResourceCategory.Cache,
                "Azure Cache for Redis",
                ImportSupportLevel.Supported),

            ["Microsoft.Cache/redisEnterprise"] = new MappedResource(
                "Microsoft.Cache/redisEnterprise",
                "AddAzureRedis",
                "Aspire.Hosting.Azure.Redis",
                ResourceCategory.Cache,
                "Azure Cache for Redis Enterprise",
                ImportSupportLevel.Supported),

            // Management (7)
            ["Microsoft.KeyVault/vaults"] = new MappedResource(
                "Microsoft.KeyVault/vaults",
                "AddAzureKeyVault",
                "Aspire.Hosting.Azure.KeyVault",
                ResourceCategory.Management,
                "Azure Key Vault",
                ImportSupportLevel.Supported),

            ["Microsoft.Insights/components"] = new MappedResource(
                "Microsoft.Insights/components",
                "AddAzureApplicationInsights",
                "Aspire.Hosting.Azure.ApplicationInsights",
                ResourceCategory.Management,
                "Azure Application Insights",
                ImportSupportLevel.Supported),

            ["Microsoft.OperationalInsights/workspaces"] = new MappedResource(
                "Microsoft.OperationalInsights/workspaces",
                "AddAzureLogAnalyticsWorkspace",
                "Aspire.Hosting.Azure.OperationalInsights",
                ResourceCategory.Management,
                "Azure Log Analytics Workspace",
                ImportSupportLevel.Supported),

            ["Microsoft.AppConfiguration/configurationStores"] = new MappedResource(
                "Microsoft.AppConfiguration/configurationStores",
                "AddAzureAppConfiguration",
                "Aspire.Hosting.Azure.AppConfiguration",
                ResourceCategory.Management,
                "Azure App Configuration",
                ImportSupportLevel.Supported),

            ["Microsoft.Search/searchServices"] = new MappedResource(
                "Microsoft.Search/searchServices",
                "AddAzureSearch",
                "Aspire.Hosting.Azure.Search",
                ResourceCategory.Management,
                "Azure AI Search",
                ImportSupportLevel.Supported),

            ["Microsoft.ContainerRegistry/registries"] = new MappedResource(
                "Microsoft.ContainerRegistry/registries",
                "AddAzureContainerRegistry",
                "Aspire.Hosting.Azure.ContainerRegistry",
                ResourceCategory.Management,
                "Azure Container Registry",
                ImportSupportLevel.Supported),

            ["Microsoft.CognitiveServices/accounts"] = new MappedResource(
                "Microsoft.CognitiveServices/accounts",
                "AddAzureOpenAI",
                "Aspire.Hosting.Azure.CognitiveServices",
                ResourceCategory.Management,
                "Azure OpenAI / Cognitive Services",
                ImportSupportLevel.Supported),

            // Compute / Placeholder (3)
            ["Microsoft.App/containerApps"] = new MappedResource(
                "Microsoft.App/containerApps",
                "AddProject",
                "Aspire.Hosting",
                ResourceCategory.Compute,
                "Azure Container App",
                ImportSupportLevel.Placeholder),

            ["Microsoft.Web/sites"] = new MappedResource(
                "Microsoft.Web/sites",
                "AddProject",
                "Aspire.Hosting",
                ResourceCategory.Compute,
                "Azure App Service",
                ImportSupportLevel.Placeholder),

            ["Microsoft.Web/sites/functions"] = new MappedResource(
                "Microsoft.Web/sites/functions",
                "AddProject",
                "Aspire.Hosting",
                ResourceCategory.Compute,
                "Azure Functions",
                ImportSupportLevel.Placeholder),
        };

        return mappings.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
