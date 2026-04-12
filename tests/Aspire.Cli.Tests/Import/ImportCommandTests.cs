// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Import;
using Aspire.Cli.Tests.Utils;
using Microsoft.AspNetCore.InternalTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspire.Cli.Tests.Import;

public class ImportCommandTests(ITestOutputHelper outputHelper)
{
    private static readonly string[] s_allImportFeatures =
    [
        KnownFeatures.ImportAzureCommandEnabled,
        KnownFeatures.ImportBicepCommandEnabled,
        KnownFeatures.ImportTerraformCommandEnabled,
    ];

    [Fact]
    public async Task ImportCommand_Help_Works()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var services = CliTestHelper.CreateServiceCollection(workspace, outputHelper, options =>
        {
            options.EnabledFeatures = s_allImportFeatures;
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<Aspire.Cli.Commands.RootCommand>();
        var result = command.Parse("import --help");

        var exitCode = await result.InvokeAsync().DefaultTimeout();

        Assert.Equal(ExitCodeConstants.Success, exitCode);
    }

    [Fact]
    public async Task ImportAzureCommand_Help_Works()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var services = CliTestHelper.CreateServiceCollection(workspace, outputHelper, options =>
        {
            options.EnabledFeatures = s_allImportFeatures;
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<Aspire.Cli.Commands.RootCommand>();
        var result = command.Parse("import azure --help");

        var exitCode = await result.InvokeAsync().DefaultTimeout();

        Assert.Equal(ExitCodeConstants.Success, exitCode);
    }

    [Fact]
    public async Task ImportBicepCommand_Help_Works()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var services = CliTestHelper.CreateServiceCollection(workspace, outputHelper, options =>
        {
            options.EnabledFeatures = s_allImportFeatures;
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<Aspire.Cli.Commands.RootCommand>();
        var result = command.Parse("import bicep --help");

        var exitCode = await result.InvokeAsync().DefaultTimeout();

        Assert.Equal(ExitCodeConstants.Success, exitCode);
    }

    [Fact]
    public async Task ImportTerraformCommand_Help_Works()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var services = CliTestHelper.CreateServiceCollection(workspace, outputHelper, options =>
        {
            options.EnabledFeatures = s_allImportFeatures;
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<Aspire.Cli.Commands.RootCommand>();
        var result = command.Parse("import terraform --help");

        var exitCode = await result.InvokeAsync().DefaultTimeout();

        Assert.Equal(ExitCodeConstants.Success, exitCode);
    }

    [Fact]
    public async Task ImportAzure_E2E_WithFakeDiscovery_GeneratesFiles()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var outputDir = Path.Combine(workspace.WorkspaceRoot.FullName, "GeneratedAppHost");

        var services = CliTestHelper.CreateServiceCollection(workspace, outputHelper, options =>
        {
            options.EnabledFeatures = s_allImportFeatures;
        });

        services.RemoveAll<IAzureResourceDiscoveryService>();
        services.AddSingleton<IAzureResourceDiscoveryService>(new FakeDiscoveryService(
        [
            new DiscoveredResource("my-cosmos", "Microsoft.DocumentDB/databaseAccounts", null, "eastus",
                "/sub/rg/providers/Microsoft.DocumentDB/databaseAccounts/my-cosmos", ResourceProvider.Azure, null),
            new DiscoveredResource("my-redis", "Microsoft.Cache/redis", null, "eastus",
                "/sub/rg/providers/Microsoft.Cache/redis/my-redis", ResourceProvider.Azure, null),
        ]));

        services.RemoveAll<IImportCommandPrompter>();
        services.AddSingleton<IImportCommandPrompter>(new FakePrompter());

        var provider = services.BuildServiceProvider();
        var command = provider.GetRequiredService<Aspire.Cli.Commands.RootCommand>();

        var result = command.Parse($"import azure --subscription sub1 --resource-group rg1 --output {outputDir} --name TestAppHost");

        var exitCode = await result.InvokeAsync().DefaultTimeout();

        Assert.Equal(ExitCodeConstants.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(outputDir, "Program.cs")), "Program.cs should be generated");
        Assert.True(File.Exists(Path.Combine(outputDir, "TestAppHost.csproj")), "TestAppHost.csproj should be generated");

        var programCs = await File.ReadAllTextAsync(Path.Combine(outputDir, "Program.cs"));
        outputHelper.WriteLine(programCs);
        Assert.Contains("AddAzureCosmosDB", programCs);
        Assert.Contains("AddAzureRedis", programCs);
    }

    [Fact]
    public async Task ImportAzure_EmptyDiscovery_ReturnsSuccessNoFiles()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        var outputDir = Path.Combine(workspace.WorkspaceRoot.FullName, "EmptyAppHost");

        var services = CliTestHelper.CreateServiceCollection(workspace, outputHelper, options =>
        {
            options.EnabledFeatures = s_allImportFeatures;
        });

        services.RemoveAll<IAzureResourceDiscoveryService>();
        services.AddSingleton<IAzureResourceDiscoveryService>(new FakeDiscoveryService([]));

        services.RemoveAll<IImportCommandPrompter>();
        services.AddSingleton<IImportCommandPrompter>(new FakePrompter());

        var provider = services.BuildServiceProvider();
        var command = provider.GetRequiredService<Aspire.Cli.Commands.RootCommand>();

        var result = command.Parse($"import azure --subscription sub1 --resource-group rg1 --output {outputDir}");

        var exitCode = await result.InvokeAsync().DefaultTimeout();

        Assert.Equal(ExitCodeConstants.Success, exitCode);
        Assert.False(Directory.Exists(outputDir), "Output directory should not be created for empty discovery");
    }

    private sealed class FakeDiscoveryService(IReadOnlyList<DiscoveredResource> resources) : IAzureResourceDiscoveryService
    {
        public Task<IReadOnlyList<(string Id, string Name)>> GetSubscriptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<(string Id, string Name)>>([(Id: "sub1", Name: "Test Subscription")]);

        public Task<IReadOnlyList<(string Name, string Location)>> GetResourceGroupsAsync(string subscriptionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<(string Name, string Location)>>([(Name: "rg1", Location: "eastus")]);

        public Task<IReadOnlyList<DiscoveredResource>> DiscoverResourcesAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken) =>
            Task.FromResult(resources);
    }

    private sealed class FakePrompter : IImportCommandPrompter
    {
        public Task<string> PromptForSubscriptionAsync(IReadOnlyList<(string Id, string Name)> subscriptions, CancellationToken ct) =>
            Task.FromResult(subscriptions[0].Id);

        public Task<string> PromptForResourceGroupAsync(IReadOnlyList<(string Name, string Location)> resourceGroups, CancellationToken ct) =>
            Task.FromResult(resourceGroups[0].Name);

        public Task<IReadOnlyList<T>> PromptForResourceSelectionAsync<T>(string promptText, IReadOnlyList<T> resources, Func<T, string> formatter, CancellationToken ct) where T : notnull =>
            Task.FromResult(resources);

        public Task<bool> ConfirmImportAsync(int resourceCount, string outputPath, CancellationToken ct) =>
            Task.FromResult(true);
    }
}
