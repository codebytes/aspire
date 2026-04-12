// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Import;

namespace Aspire.Cli.Tests.Import;

public class TerraformToArmTypeMappingTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void TryGetArmType_KnownType_MapsCorrectly()
    {
        var result = TerraformToArmTypeMapping.TryGetArmType("azurerm_cosmosdb_account", out var armType);

        Assert.True(result);
        Assert.Equal("Microsoft.DocumentDB/databaseAccounts", armType);
    }

    [Fact]
    public void TryGetArmType_CaseInsensitive()
    {
        var result = TerraformToArmTypeMapping.TryGetArmType("AZURERM_COSMOSDB_ACCOUNT", out var armType);

        Assert.True(result);
        Assert.Equal("Microsoft.DocumentDB/databaseAccounts", armType);
    }

    [Fact]
    public void TryGetArmType_UnknownType_ReturnsFalse()
    {
        var result = TerraformToArmTypeMapping.TryGetArmType("azurerm_nonexistent_resource", out var armType);

        Assert.False(result);
        Assert.Null(armType);
    }

    [Fact]
    public void GetAllMappings_HasAtLeast20Entries()
    {
        var mappings = TerraformToArmTypeMapping.GetAllMappings();

        outputHelper.WriteLine($"Total Terraform-to-ARM mappings: {mappings.Count}");

        Assert.True(mappings.Count >= 20, $"Expected at least 20 mappings but found {mappings.Count}");
    }

    [Fact]
    public void AllArmTypes_ExistInAzureResourceMapper()
    {
        var azureMapper = new AzureResourceMapper();
        var azureMappings = azureMapper.GetAllMappings();
        var terraformMappings = TerraformToArmTypeMapping.GetAllMappings();

        foreach (var (terraformType, armType) in terraformMappings)
        {
            Assert.True(
                azureMappings.ContainsKey(armType),
                $"Terraform type '{terraformType}' maps to ARM type '{armType}' which is missing from AzureResourceMapper");

            outputHelper.WriteLine($"{terraformType} → {armType} ✓");
        }
    }

    [Theory]
    [InlineData("azurerm_mssql_server", "Microsoft.Sql/servers")]
    [InlineData("azurerm_servicebus_namespace", "Microsoft.ServiceBus/namespaces")]
    [InlineData("azurerm_storage_account", "Microsoft.Storage/storageAccounts")]
    [InlineData("azurerm_redis_cache", "Microsoft.Cache/redis")]
    [InlineData("azurerm_key_vault", "Microsoft.KeyVault/vaults")]
    [InlineData("azurerm_container_app", "Microsoft.App/containerApps")]
    [InlineData("azurerm_linux_web_app", "Microsoft.Web/sites")]
    [InlineData("azurerm_linux_function_app", "Microsoft.Web/sites/functions")]
    public void TryGetArmType_SpecificMappings(string terraformType, string expectedArmType)
    {
        var result = TerraformToArmTypeMapping.TryGetArmType(terraformType, out var armType);

        Assert.True(result, $"Expected {terraformType} to map successfully");
        Assert.Equal(expectedArmType, armType);
    }
}
