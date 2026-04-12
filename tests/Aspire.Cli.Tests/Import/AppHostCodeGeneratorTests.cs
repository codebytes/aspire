// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Import;

namespace Aspire.Cli.Tests.Import;

public class AppHostCodeGeneratorTests(ITestOutputHelper outputHelper)
{
    private static readonly ImportedResource s_cosmosResource = new(
        Name: "cosmosDb",
        SourceResourceName: "cosmos-db",
        SourceType: "Microsoft.DocumentDB/databaseAccounts",
        Kind: null,
        Category: ResourceCategory.Database,
        AspireBuilderMethod: "AddAzureCosmosDB",
        NuGetPackage: "Aspire.Hosting.Azure.CosmosDB",
        FriendlyName: "Azure Cosmos DB",
        SupportLevel: ImportSupportLevel.Supported);

    private static readonly ImportedResource s_redisResource = new(
        Name: "redis",
        SourceResourceName: "my-redis",
        SourceType: "Microsoft.Cache/redis",
        Kind: null,
        Category: ResourceCategory.Cache,
        AspireBuilderMethod: "AddAzureRedis",
        NuGetPackage: "Aspire.Hosting.Azure.Redis",
        FriendlyName: "Azure Cache for Redis",
        SupportLevel: ImportSupportLevel.Supported);

    private static readonly ImportedResource s_containerAppResource = new(
        Name: "myApp",
        SourceResourceName: "my-app",
        SourceType: "Microsoft.App/containerApps",
        Kind: null,
        Category: ResourceCategory.Compute,
        AspireBuilderMethod: "AddProject",
        NuGetPackage: "Aspire.Hosting",
        FriendlyName: "Azure Container App",
        SupportLevel: ImportSupportLevel.Placeholder);

    private static readonly ImportedResource s_unsupportedResource = new(
        Name: "myVnet",
        SourceResourceName: "my-vnet",
        SourceType: "Microsoft.Network/virtualNetworks",
        Kind: null,
        Category: ResourceCategory.Other,
        AspireBuilderMethod: null,
        NuGetPackage: null,
        FriendlyName: null,
        SupportLevel: ImportSupportLevel.Unsupported);

    [Fact]
    public void GenerateProgramCs_ExistingMode_EmitsAsExisting()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_cosmosResource, s_redisResource };

        var code = generator.GenerateProgramCs(resources, "test-source", ImportMode.Existing);

        outputHelper.WriteLine(code);

        Assert.Contains(".AsExisting(", code);
        Assert.Contains("AddAzureCosmosDB(\"cosmos-db\")", code);
        Assert.Contains("AddAzureRedis(\"my-redis\")", code);
        Assert.Contains("mode: existing", code);
        Assert.Contains("builder.Build().Run();", code);
    }

    [Fact]
    public void GenerateProgramCs_NewMode_NoAsExisting()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_cosmosResource, s_redisResource };

        var code = generator.GenerateProgramCs(resources, "test-source", ImportMode.New);

        outputHelper.WriteLine(code);

        Assert.DoesNotContain(".AsExisting(", code);
        Assert.Contains("AddAzureCosmosDB(\"cosmos-db\")", code);
        Assert.Contains("AddAzureRedis(\"my-redis\")", code);
        Assert.Contains("mode: new", code);
    }

    [Fact]
    public void GenerateProgramCs_PlaceholderResources_EmitTodoComments_ExistingMode()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_containerAppResource };

        var code = generator.GenerateProgramCs(resources, "source", ImportMode.Existing);

        outputHelper.WriteLine(code);

        Assert.Contains("TODO", code);
        Assert.Contains("my-app", code);
        Assert.Contains("Compute / Workloads (TODO)", code);
    }

    [Fact]
    public void GenerateProgramCs_PlaceholderResources_EmitTodoComments_NewMode()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_containerAppResource };

        var code = generator.GenerateProgramCs(resources, "source", ImportMode.New);

        outputHelper.WriteLine(code);

        Assert.Contains("TODO", code);
        Assert.Contains("my-app", code);
    }

    [Fact]
    public void GenerateProgramCs_UnsupportedResources_EmitComments()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_unsupportedResource };

        var code = generator.GenerateProgramCs(resources, "source");

        outputHelper.WriteLine(code);

        Assert.Contains("Unsupported resources", code);
        Assert.Contains("my-vnet", code);
        Assert.Contains("Microsoft.Network/virtualNetworks", code);
    }

    [Fact]
    public void GenerateProgramCs_EmptyList_ProducesValidProgramCs()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource>();

        var code = generator.GenerateProgramCs(resources, "empty-source");

        outputHelper.WriteLine(code);

        Assert.Contains("var builder = DistributedApplication.CreateBuilder(args);", code);
        Assert.Contains("builder.Build().Run();", code);
        Assert.DoesNotContain("AddAzure", code);
    }

    [Theory]
    [InlineData("my-resource", "myResource")]
    [InlineData("my_resource", "myResource")]
    [InlineData("my-long-name", "myLongName")]
    [InlineData("1starts-with-digit", "r1startsWithDigit")]
    [InlineData("class", "@class")]
    [InlineData("int", "@int")]
    [InlineData("", "resource")]
    [InlineData("   ", "resource")]
    [InlineData("simple", "simple")]
    [InlineData("my.dotted.name", "myDottedName")]
    public void SanitizeResourceName_VariousInputs(string input, string expected)
    {
        var result = AppHostCodeGenerator.SanitizeResourceName(input);

        outputHelper.WriteLine($"SanitizeResourceName(\"{input}\") = \"{result}\"");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateCsproj_IncludesAspireAppHostSdk()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_cosmosResource, s_redisResource };

        var csproj = generator.GenerateCsproj(resources, "TestAppHost");

        outputHelper.WriteLine(csproj);

        Assert.Contains("Aspire.AppHost.Sdk", csproj);
        Assert.Contains("Aspire.Hosting.Azure.CosmosDB", csproj);
        Assert.Contains("Aspire.Hosting.Azure.Redis", csproj);
        Assert.Contains("<IsAspireHost>true</IsAspireHost>", csproj);
    }

    [Fact]
    public void GenerateCsproj_DeduplicatesPackages()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource>
        {
            s_cosmosResource,
            s_cosmosResource with { Name = "cosmos2", SourceResourceName = "cosmos-db-2" },
        };

        var csproj = generator.GenerateCsproj(resources, "TestAppHost");

        outputHelper.WriteLine(csproj);

        var occurrences = csproj.Split("Aspire.Hosting.Azure.CosmosDB").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void GenerateCsproj_SortsPackages()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_redisResource, s_cosmosResource };

        var csproj = generator.GenerateCsproj(resources, "TestAppHost");

        outputHelper.WriteLine(csproj);

        var cosmosIndex = csproj.IndexOf("Aspire.Hosting.Azure.CosmosDB", StringComparison.Ordinal);
        var redisIndex = csproj.IndexOf("Aspire.Hosting.Azure.Redis", StringComparison.Ordinal);
        Assert.True(cosmosIndex < redisIndex, "Packages should be sorted alphabetically");
    }

    [Fact]
    public void GenerateProgramCs_SectionHeaders_GroupByCategory()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource>
        {
            s_cosmosResource,
            s_redisResource,
            s_containerAppResource,
            s_unsupportedResource
        };

        var code = generator.GenerateProgramCs(resources, "test-source");

        outputHelper.WriteLine(code);

        Assert.Contains("Azure Cosmos DB", code);
        Assert.Contains("Azure Cache for Redis", code);
        Assert.Contains("Compute / Workloads (TODO)", code);
        Assert.Contains("Unsupported resources", code);
    }

    [Fact]
    public void FormatSectionHeader_ProducesExpectedFormat()
    {
        var header = AppHostCodeGenerator.FormatSectionHeader("My Section");

        outputHelper.WriteLine(header);

        Assert.StartsWith("// ── My Section ", header);
        Assert.Contains("─", header);
    }

    [Fact]
    public void GenerateCsproj_ExcludesUnsupportedAndPlaceholderPackages()
    {
        var generator = new AppHostCodeGenerator();
        var resources = new List<ImportedResource> { s_unsupportedResource, s_containerAppResource };

        var csproj = generator.GenerateCsproj(resources, "TestAppHost");

        outputHelper.WriteLine(csproj);

        Assert.DoesNotContain("Aspire.Hosting.Azure", csproj);
    }
}
