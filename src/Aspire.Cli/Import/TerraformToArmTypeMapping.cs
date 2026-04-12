// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;

namespace Aspire.Cli.Import;

/// <summary>
/// Provides a static mapping from Terraform <c>azurerm_*</c> resource types to Azure ARM resource type identifiers.
/// </summary>
internal static class TerraformToArmTypeMapping
{
    private static readonly FrozenDictionary<string, string> s_mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Database
        ["azurerm_cosmosdb_account"] = "Microsoft.DocumentDB/databaseAccounts",
        ["azurerm_mssql_server"] = "Microsoft.Sql/servers",
        ["azurerm_postgresql_flexible_server"] = "Microsoft.DBforPostgreSQL/flexibleServers",
        ["azurerm_kusto_cluster"] = "Microsoft.Kusto/clusters",

        // Messaging
        ["azurerm_servicebus_namespace"] = "Microsoft.ServiceBus/namespaces",
        ["azurerm_eventhub_namespace"] = "Microsoft.EventHub/namespaces",
        ["azurerm_signalr_service"] = "Microsoft.SignalRService/signalR",
        ["azurerm_web_pubsub"] = "Microsoft.SignalRService/webPubSub",

        // Storage
        ["azurerm_storage_account"] = "Microsoft.Storage/storageAccounts",

        // Cache
        ["azurerm_redis_cache"] = "Microsoft.Cache/redis",
        ["azurerm_redis_enterprise_cluster"] = "Microsoft.Cache/redisEnterprise",

        // Management
        ["azurerm_key_vault"] = "Microsoft.KeyVault/vaults",
        ["azurerm_application_insights"] = "Microsoft.Insights/components",
        ["azurerm_log_analytics_workspace"] = "Microsoft.OperationalInsights/workspaces",
        ["azurerm_app_configuration"] = "Microsoft.AppConfiguration/configurationStores",
        ["azurerm_search_service"] = "Microsoft.Search/searchServices",
        ["azurerm_container_registry"] = "Microsoft.ContainerRegistry/registries",
        ["azurerm_cognitive_account"] = "Microsoft.CognitiveServices/accounts",

        // Compute
        ["azurerm_container_app"] = "Microsoft.App/containerApps",
        ["azurerm_linux_web_app"] = "Microsoft.Web/sites",
        ["azurerm_windows_web_app"] = "Microsoft.Web/sites",
        ["azurerm_linux_function_app"] = "Microsoft.Web/sites/functions",
        ["azurerm_windows_function_app"] = "Microsoft.Web/sites/functions",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to resolve a Terraform resource type to its ARM equivalent.
    /// </summary>
    /// <param name="terraformType">The Terraform <c>azurerm_*</c> resource type.</param>
    /// <param name="armType">The corresponding ARM resource type, or <c>null</c> if not found.</param>
    /// <returns><c>true</c> if the Terraform type was mapped; otherwise, <c>false</c>.</returns>
    public static bool TryGetArmType(string terraformType, out string? armType)
    {
        return s_mappings.TryGetValue(terraformType, out armType);
    }

    /// <summary>
    /// Gets all known Terraform-to-ARM type mappings.
    /// </summary>
    /// <returns>A read-only dictionary of all mappings.</returns>
    public static IReadOnlyDictionary<string, string> GetAllMappings() => s_mappings;
}
