// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Import;

namespace Aspire.Cli.Tests.Import;

public class BicepFileParserTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void ParseArmJsonResources_Simple2Resources_ExtractsCorrectly()
    {
        var armJson = """
        {
          "resources": [
            {
              "type": "Microsoft.DocumentDB/databaseAccounts",
              "name": "my-cosmos",
              "location": "eastus",
              "kind": "GlobalDocumentDB"
            },
            {
              "type": "Microsoft.Storage/storageAccounts",
              "name": "mystorage",
              "location": "westus2"
            }
          ]
        }
        """;

        var resources = BicepFileParser.ParseArmJsonResources(armJson);

        Assert.Equal(2, resources.Count);
        Assert.Equal("Microsoft.DocumentDB/databaseAccounts", resources[0].SourceType);
        Assert.Equal("my-cosmos", resources[0].Name);
        Assert.Equal("eastus", resources[0].Location);
        Assert.Equal("GlobalDocumentDB", resources[0].Kind);
        Assert.Equal(ResourceProvider.Azure, resources[0].Provider);

        Assert.Equal("Microsoft.Storage/storageAccounts", resources[1].SourceType);
        Assert.Equal("mystorage", resources[1].Name);
        Assert.Null(resources[1].Kind);
    }

    [Fact]
    public void ParseArmJsonResources_ApiVersionStripping_Works()
    {
        var armJson = """
        {
          "resources": [
            {
              "type": "Microsoft.Cache/redis@2023-08-01",
              "name": "my-redis",
              "location": "eastus"
            }
          ]
        }
        """;

        var resources = BicepFileParser.ParseArmJsonResources(armJson);

        Assert.Single(resources);
        Assert.Equal("Microsoft.Cache/redis", resources[0].SourceType);
        Assert.Equal("my-redis", resources[0].Name);
    }

    [Fact]
    public void ParseArmJsonResources_NestedDeployments_Recurse()
    {
        var armJson = """
        {
          "resources": [
            {
              "type": "Microsoft.Resources/deployments",
              "name": "nestedDeployment",
              "properties": {
                "template": {
                  "resources": [
                    {
                      "type": "Microsoft.ServiceBus/namespaces",
                      "name": "my-sb",
                      "location": "centralus"
                    }
                  ]
                }
              }
            },
            {
              "type": "Microsoft.KeyVault/vaults",
              "name": "my-kv",
              "location": "eastus"
            }
          ]
        }
        """;

        var resources = BicepFileParser.ParseArmJsonResources(armJson);

        outputHelper.WriteLine($"Found {resources.Count} resources");
        foreach (var r in resources)
        {
            outputHelper.WriteLine($"  {r.SourceType}: {r.Name}");
        }

        Assert.Equal(3, resources.Count);

        Assert.Equal("Microsoft.Resources/deployments", resources[0].SourceType);
        Assert.Equal("Microsoft.ServiceBus/namespaces", resources[1].SourceType);
        Assert.Equal("my-sb", resources[1].Name);
        Assert.Equal("Microsoft.KeyVault/vaults", resources[2].SourceType);
    }

    [Fact]
    public void ParseArmJsonResources_InvalidJson_ReturnsEmpty()
    {
        var invalidJson = "this is not valid json {{{";

        var resources = BicepFileParser.ParseArmJsonResources(invalidJson);

        Assert.Empty(resources);
    }

    [Fact]
    public void ParseArmJsonResources_Tags_Extracted()
    {
        var armJson = """
        {
          "resources": [
            {
              "type": "Microsoft.Storage/storageAccounts",
              "name": "tagged-storage",
              "location": "eastus",
              "tags": {
                "environment": "production",
                "team": "platform"
              }
            }
          ]
        }
        """;

        var resources = BicepFileParser.ParseArmJsonResources(armJson);

        Assert.Single(resources);
        Assert.NotNull(resources[0].Tags);
        Assert.Equal(2, resources[0].Tags!.Count);
        Assert.Equal("production", resources[0].Tags!["environment"]);
        Assert.Equal("platform", resources[0].Tags!["team"]);
    }

    [Fact]
    public void E2E_RealisticApp_FullPipeline()
    {
        var armJson = """
        {
          "resources": [
            {
              "type": "Microsoft.DocumentDB/databaseAccounts@2023-11-15",
              "name": "app-cosmos",
              "location": "eastus",
              "kind": "GlobalDocumentDB"
            },
            {
              "type": "Microsoft.Storage/storageAccounts@2023-01-01",
              "name": "appstorage",
              "location": "eastus"
            },
            {
              "type": "Microsoft.Cache/redis@2023-08-01",
              "name": "app-redis",
              "location": "eastus"
            },
            {
              "type": "Microsoft.ServiceBus/namespaces@2022-10-01-preview",
              "name": "app-sb",
              "location": "eastus"
            },
            {
              "type": "Microsoft.KeyVault/vaults@2023-07-01",
              "name": "app-kv",
              "location": "eastus"
            },
            {
              "type": "Microsoft.App/containerApps@2023-05-01",
              "name": "app-frontend",
              "location": "eastus"
            },
            {
              "type": "Microsoft.Network/virtualNetworks@2023-05-01",
              "name": "app-vnet",
              "location": "eastus"
            }
          ]
        }
        """;

        var discovered = BicepFileParser.ParseArmJsonResources(armJson);
        Assert.True(discovered.Count >= 7, $"Expected at least 7 resources, got {discovered.Count}");

        var mapper = new AzureResourceMapper();
        var mappingService = new ResourceMappingService([mapper]);
        var mapped = mappingService.MapAll(discovered);

        Assert.Equal(discovered.Count, mapped.Count);

        var generator = new AppHostCodeGenerator();
        var programCs = generator.GenerateProgramCs(mapped, "test-bicep-source", ImportMode.Existing);

        outputHelper.WriteLine(programCs);

        Assert.Contains(".AsExisting(", programCs);
        Assert.Contains("AddAzureCosmosDB", programCs);
        Assert.Contains("AddAzureStorage", programCs);
        Assert.Contains("AddAzureRedis", programCs);
        Assert.Contains("AddAzureServiceBus", programCs);
        Assert.Contains("AddAzureKeyVault", programCs);

        Assert.Contains("TODO", programCs);
        Assert.Contains("app-frontend", programCs);

        Assert.Contains("Unsupported resources", programCs);
        Assert.Contains("app-vnet", programCs);

        var csproj = generator.GenerateCsproj(mapped, "TestAppHost");

        outputHelper.WriteLine(csproj);

        Assert.Contains("Aspire.Hosting.Azure.CosmosDB", csproj);
        Assert.Contains("Aspire.Hosting.Azure.Storage", csproj);
        Assert.Contains("Aspire.Hosting.Azure.Redis", csproj);
        Assert.Contains("Aspire.Hosting.Azure.ServiceBus", csproj);
        Assert.Contains("Aspire.Hosting.Azure.KeyVault", csproj);
    }

    [Fact]
    public void E2E_NestedModules_AllResourcesFlattened()
    {
        var armJson = """
        {
          "resources": [
            {
              "type": "Microsoft.DocumentDB/databaseAccounts@2023-11-15",
              "name": "top-cosmos",
              "location": "eastus"
            },
            {
              "type": "Microsoft.Resources/deployments",
              "name": "level1-deployment",
              "properties": {
                "template": {
                  "resources": [
                    {
                      "type": "Microsoft.Storage/storageAccounts@2023-01-01",
                      "name": "level1-storage",
                      "location": "eastus"
                    },
                    {
                      "type": "Microsoft.Resources/deployments",
                      "name": "level2-deployment",
                      "properties": {
                        "template": {
                          "resources": [
                            {
                              "type": "Microsoft.Cache/redis@2023-08-01",
                              "name": "level2-redis",
                              "location": "eastus"
                            }
                          ]
                        }
                      }
                    }
                  ]
                }
              }
            }
          ]
        }
        """;

        var discovered = BicepFileParser.ParseArmJsonResources(armJson);

        outputHelper.WriteLine($"Found {discovered.Count} resources across all nesting levels");
        foreach (var r in discovered)
        {
            outputHelper.WriteLine($"  {r.SourceType}: {r.Name}");
        }

        var nonDeploymentResources = discovered
            .Where(r => !r.SourceType.Equals("Microsoft.Resources/deployments", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(3, nonDeploymentResources.Count);
        Assert.Contains(nonDeploymentResources, r => r.Name == "top-cosmos");
        Assert.Contains(nonDeploymentResources, r => r.Name == "level1-storage");
        Assert.Contains(nonDeploymentResources, r => r.Name == "level2-redis");
    }

    [Fact]
    public void ParseArmJsonResources_EmptyResourcesArray_ReturnsEmpty()
    {
        var armJson = """
        {
          "resources": []
        }
        """;

        var resources = BicepFileParser.ParseArmJsonResources(armJson);

        Assert.Empty(resources);
    }

    [Fact]
    public void ParseArmJsonResources_NoResourcesProperty_ReturnsEmpty()
    {
        var armJson = """
        {
          "parameters": {},
          "variables": {}
        }
        """;

        var resources = BicepFileParser.ParseArmJsonResources(armJson);

        Assert.Empty(resources);
    }
}
