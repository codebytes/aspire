// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Import;

namespace Aspire.Cli.Tests.Import;

public class AzureResourceMapperTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void TryMap_KnownAzureType_ReturnsSupportedMapping()
    {
        var mapper = new AzureResourceMapper();
        var resource = new DiscoveredResource(
            Name: "my-cosmos",
            SourceType: "Microsoft.DocumentDB/databaseAccounts",
            Kind: null,
            Location: "eastus",
            SourceAddress: "/subscriptions/sub/providers/Microsoft.DocumentDB/databaseAccounts/my-cosmos",
            Provider: ResourceProvider.Azure,
            Tags: null);

        var result = mapper.TryMap(resource, out var mapping);

        Assert.True(result);
        Assert.NotNull(mapping);
        Assert.Equal("AddAzureCosmosDB", mapping.AspireBuilderMethod);
        Assert.Equal("Aspire.Hosting.Azure.CosmosDB", mapping.NuGetPackage);
        Assert.Equal(ImportSupportLevel.Supported, mapping.SupportLevel);
        Assert.Equal(ResourceCategory.Database, mapping.Category);
        Assert.Equal("Azure Cosmos DB", mapping.FriendlyName);

        outputHelper.WriteLine($"Mapped {resource.SourceType} → {mapping.AspireBuilderMethod}");
    }

    [Fact]
    public void TryMap_ComputeType_ReturnsPlaceholderMapping()
    {
        var mapper = new AzureResourceMapper();
        var resource = new DiscoveredResource(
            Name: "my-app",
            SourceType: "Microsoft.App/containerApps",
            Kind: null,
            Location: "westus2",
            SourceAddress: "/subscriptions/sub/providers/Microsoft.App/containerApps/my-app",
            Provider: ResourceProvider.Azure,
            Tags: null);

        var result = mapper.TryMap(resource, out var mapping);

        Assert.True(result);
        Assert.NotNull(mapping);
        Assert.Equal(ImportSupportLevel.Placeholder, mapping.SupportLevel);
        Assert.Equal(ResourceCategory.Compute, mapping.Category);
        Assert.Equal("AddProject", mapping.AspireBuilderMethod);
    }

    [Fact]
    public void TryMap_UnknownType_ReturnsFalse()
    {
        var mapper = new AzureResourceMapper();
        var resource = new DiscoveredResource(
            Name: "my-thing",
            SourceType: "Microsoft.FakeProvider/somethingUnknown",
            Kind: null,
            Location: null,
            SourceAddress: "/subscriptions/sub/providers/Microsoft.FakeProvider/somethingUnknown/my-thing",
            Provider: ResourceProvider.Azure,
            Tags: null);

        var result = mapper.TryMap(resource, out var mapping);

        Assert.False(result);
        Assert.Null(mapping);
    }

    [Fact]
    public void TryMap_CaseInsensitiveLookup_Works()
    {
        var mapper = new AzureResourceMapper();
        var resource = new DiscoveredResource(
            Name: "my-redis",
            SourceType: "microsoft.cache/REDIS",
            Kind: null,
            Location: "eastus",
            SourceAddress: "/subscriptions/sub/providers/Microsoft.Cache/redis/my-redis",
            Provider: ResourceProvider.Azure,
            Tags: null);

        var result = mapper.TryMap(resource, out var mapping);

        Assert.True(result);
        Assert.NotNull(mapping);
        Assert.Equal("AddAzureRedis", mapping.AspireBuilderMethod);
    }

    [Fact]
    public void GetAllMappings_Returns21Entries()
    {
        var mapper = new AzureResourceMapper();
        var mappings = mapper.GetAllMappings();

        Assert.Equal(21, mappings.Count);
    }

    [Fact]
    public void GetAllMappings_AllEntriesHaveNonNullFields()
    {
        var mapper = new AzureResourceMapper();
        var mappings = mapper.GetAllMappings();

        foreach (var (key, value) in mappings)
        {
            Assert.False(string.IsNullOrWhiteSpace(key), "Key should not be null or whitespace");
            Assert.False(string.IsNullOrWhiteSpace(value.SourceType), $"SourceType for {key} should not be null");
            Assert.False(string.IsNullOrWhiteSpace(value.AspireBuilderMethod), $"AspireBuilderMethod for {key} should not be null");
            Assert.False(string.IsNullOrWhiteSpace(value.NuGetPackage), $"NuGetPackage for {key} should not be null");
            Assert.False(string.IsNullOrWhiteSpace(value.FriendlyName), $"FriendlyName for {key} should not be null");
        }
    }

    [Fact]
    public void ResourceMappingService_MapAll_HandlesMixedResources()
    {
        var mapper = new AzureResourceMapper();
        var service = new ResourceMappingService([mapper]);

        var resources = new List<DiscoveredResource>
        {
            new("cosmos-db", "Microsoft.DocumentDB/databaseAccounts", null, "eastus",
                "addr1", ResourceProvider.Azure, null),
            new("my-app", "Microsoft.App/containerApps", null, "westus2",
                "addr2", ResourceProvider.Azure, null),
            new("unknown-thing", "Microsoft.UnknownProvider/something", null, "centralus",
                "addr3", ResourceProvider.Azure, null),
        };

        var results = service.MapAll(resources);

        Assert.Equal(3, results.Count);
        Assert.Equal(ImportSupportLevel.Supported, results[0].SupportLevel);
        Assert.Equal(ImportSupportLevel.Placeholder, results[1].SupportLevel);
        Assert.Equal(ImportSupportLevel.Unsupported, results[2].SupportLevel);

        Assert.Equal("AddAzureCosmosDB", results[0].AspireBuilderMethod);
        Assert.Null(results[2].AspireBuilderMethod);
        Assert.Equal(ResourceCategory.Other, results[2].Category);
    }

    [Fact]
    public void GetUnsupportedResourceComment_FormatsCorrectly()
    {
        var mapper = new AzureResourceMapper();
        var comment = mapper.GetUnsupportedResourceComment("Microsoft.Fake/thing", "my-resource");

        Assert.Contains("my-resource", comment);
        Assert.Contains("Microsoft.Fake/thing", comment);
        Assert.Contains("TODO", comment);
    }

    [Fact]
    public void Provider_ReturnsAzure()
    {
        var mapper = new AzureResourceMapper();
        Assert.Equal(ResourceProvider.Azure, mapper.Provider);
    }

    [Fact]
    public void TryMap_KustoCluster_UsesCorrectApiName()
    {
        var mapper = new AzureResourceMapper();
        var resource = new DiscoveredResource(
            Name: "my-kusto",
            SourceType: "Microsoft.Kusto/clusters",
            Kind: null,
            Location: "eastus",
            SourceAddress: "/subscriptions/sub/providers/Microsoft.Kusto/clusters/my-kusto",
            Provider: ResourceProvider.Azure,
            Tags: null);

        var result = mapper.TryMap(resource, out var mapping);

        Assert.True(result);
        Assert.NotNull(mapping);
        Assert.Equal("AddAzureKustoCluster", mapping.AspireBuilderMethod);
        Assert.Equal("Aspire.Hosting.Azure.Kusto", mapping.NuGetPackage);
        Assert.Equal(ImportSupportLevel.Supported, mapping.SupportLevel);

        outputHelper.WriteLine($"Mapped {resource.SourceType} → {mapping.AspireBuilderMethod}");
    }
}
