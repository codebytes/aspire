// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Import;
using Aspire.Cli.Tests.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Cli.Tests.Import;

public class TerraformFileParserTests(ITestOutputHelper outputHelper)
{
    private static TerraformFileParser CreateParser() =>
        new(NullLogger<TerraformFileParser>.Instance);

    [Fact]
    public async Task ParseAsync_SimpleResource_ParsesCorrectly()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "main.tf"), """
            resource "azurerm_cosmosdb_account" "main" {
              name     = "my-cosmos"
              location = "eastus"
            }
            """);

        var parser = CreateParser();
        var resources = await parser.ParseAsync(dir, CancellationToken.None);

        Assert.Single(resources);
        Assert.Equal("Microsoft.DocumentDB/databaseAccounts", resources[0].SourceType);
        Assert.Equal("my-cosmos", resources[0].Name);
        Assert.Equal("eastus", resources[0].Location);
        Assert.Equal(ResourceProvider.Azure, resources[0].Provider);
    }

    [Fact]
    public async Task ParseAsync_MultipleResources_FromOneFile()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "main.tf"), """
            resource "azurerm_cosmosdb_account" "cosmos" {
              name     = "my-cosmos"
              location = "eastus"
            }

            resource "azurerm_storage_account" "storage" {
              name     = "mystorage"
              location = "westus2"
            }
            """);

        var parser = CreateParser();
        var resources = await parser.ParseAsync(dir, CancellationToken.None);

        Assert.Equal(2, resources.Count);
        Assert.Equal("Microsoft.DocumentDB/databaseAccounts", resources[0].SourceType);
        Assert.Equal("Microsoft.Storage/storageAccounts", resources[1].SourceType);
    }

    [Fact]
    public async Task ParseAsync_MultipleTfFiles_Combined()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;

        await File.WriteAllTextAsync(Path.Combine(dir, "database.tf"), """
            resource "azurerm_cosmosdb_account" "cosmos" {
              name     = "my-cosmos"
              location = "eastus"
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "cache.tf"), """
            resource "azurerm_redis_cache" "redis" {
              name     = "my-redis"
              location = "westus2"
            }
            """);

        var parser = CreateParser();
        var resources = await parser.ParseAsync(dir, CancellationToken.None);

        Assert.Equal(2, resources.Count);
    }

    [Fact]
    public async Task ParseAsync_UnknownResourceType_IncludedAsIs()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "main.tf"), """
            resource "azurerm_nonexistent_thing" "weird" {
              name     = "strange-resource"
              location = "centralus"
            }
            """);

        var parser = CreateParser();
        var resources = await parser.ParseAsync(dir, CancellationToken.None);

        Assert.Single(resources);
        Assert.Equal("azurerm_nonexistent_thing", resources[0].SourceType);
        Assert.Equal("strange-resource", resources[0].Name);
        Assert.Equal(ResourceProvider.Azure, resources[0].Provider);
    }

    [Fact]
    public async Task ParseAsync_VariableRef_FallsBackToLogicalName()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "main.tf"), """
            resource "azurerm_storage_account" "mystorageacct" {
              name     = var.storage_name
              location = var.location
            }
            """);

        var parser = CreateParser();
        var resources = await parser.ParseAsync(dir, CancellationToken.None);

        Assert.Single(resources);
        Assert.Equal("mystorageacct", resources[0].Name);
    }

    [Fact]
    public async Task ParseAsync_EmptyDir_ReturnsEmpty()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;

        var parser = CreateParser();
        var resources = await parser.ParseAsync(dir, CancellationToken.None);

        Assert.Empty(resources);
    }

    [Fact]
    public async Task ParseAsync_MultiProvider_CorrectProviderEnum()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "main.tf"), """
            resource "azurerm_cosmosdb_account" "cosmos" {
              name     = "my-cosmos"
              location = "eastus"
            }

            resource "aws_s3_bucket" "bucket" {
              bucket = "my-bucket"
            }
            """);

        var parser = CreateParser();
        var resources = await parser.ParseAsync(dir, CancellationToken.None);

        Assert.Equal(2, resources.Count);

        var azureResource = resources.First(r => r.Provider == ResourceProvider.Azure);
        var awsResource = resources.First(r => r.Provider == ResourceProvider.Aws);

        Assert.Equal("Microsoft.DocumentDB/databaseAccounts", azureResource.SourceType);
        Assert.Equal("aws_s3_bucket", awsResource.SourceType);
    }

    [Fact]
    public async Task E2E_RealisticApp_MultiFile_FullPipeline()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var dir = workspace.WorkspaceRoot.FullName;

        await File.WriteAllTextAsync(Path.Combine(dir, "main.tf"), """
            resource "azurerm_cosmosdb_account" "cosmos" {
              name     = "app-cosmos"
              location = "eastus"
            }

            resource "azurerm_storage_account" "storage" {
              name     = "appstorage"
              location = "eastus"
            }

            resource "azurerm_key_vault" "kv" {
              name     = "app-kv"
              location = "eastus"
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "messaging.tf"), """
            resource "azurerm_servicebus_namespace" "sb" {
              name     = "app-sb"
              location = "eastus"
            }

            resource "azurerm_eventhub_namespace" "eh" {
              name     = "app-eh"
              location = "eastus"
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "compute.tf"), """
            resource "azurerm_container_app" "frontend" {
              name     = "app-frontend"
              location = "eastus"
            }

            resource "azurerm_linux_web_app" "api" {
              name     = "app-api"
              location = "eastus"
            }
            """);

        var parser = CreateParser();
        var discovered = await parser.ParseAsync(dir, CancellationToken.None);

        outputHelper.WriteLine($"Discovered {discovered.Count} resources");
        foreach (var r in discovered)
        {
            outputHelper.WriteLine($"  {r.SourceType}: {r.Name} (Provider: {r.Provider})");
        }

        Assert.True(discovered.Count >= 7, $"Expected at least 7 resources, got {discovered.Count}");

        var mapper = new AzureResourceMapper();
        var mappingService = new ResourceMappingService([mapper]);
        var mapped = mappingService.MapAll(discovered);

        var generator = new AppHostCodeGenerator();
        var programCs = generator.GenerateProgramCs(mapped, "terraform-dir", ImportMode.Existing);

        outputHelper.WriteLine(programCs);

        Assert.Contains("AddAzureCosmosDB", programCs);
        Assert.Contains("AddAzureStorage", programCs);
        Assert.Contains("AddAzureKeyVault", programCs);
        Assert.Contains("AddAzureServiceBus", programCs);
        Assert.Contains("AddAzureEventHubs", programCs);
        Assert.Contains(".RunAsExisting(", programCs);
        Assert.Contains("TODO", programCs);

        var csproj = generator.GenerateCsproj(mapped, "TestAppHost");
        Assert.Contains("Aspire.Hosting.Azure.CosmosDB", csproj);
        Assert.Contains("Aspire.Hosting.Azure.Storage", csproj);
    }
}
