// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Aspire.Cli.Configuration;
using Aspire.Cli.Import;
using Aspire.Cli.Interaction;
using Aspire.Cli.Resources;
using Aspire.Cli.Telemetry;
using Aspire.Cli.Utils;

namespace Aspire.Cli.Commands;

/// <summary>
/// Imports Azure resources from a live subscription and resource group into an Aspire AppHost project.
/// </summary>
internal sealed class ImportAzureCommand : BaseCommand
{
    private static readonly Option<string?> s_subscriptionOption = new("--subscription")
    {
        Description = ImportCommandStrings.SubscriptionOption,
    };

    private static readonly Option<string?> s_resourceGroupOption = new("--resource-group")
    {
        Description = ImportCommandStrings.ResourceGroupOption,
    };

    private static readonly Option<string?> s_outputOption = new("--output")
    {
        Description = ImportCommandStrings.OutputOption,
    };

    private static readonly Option<string?> s_nameOption = new("--name")
    {
        Description = ImportCommandStrings.NameOption,
    };

    private static readonly Option<string> s_modeOption = new("--mode")
    {
        Description = ImportCommandStrings.ModeOption,
        DefaultValueFactory = _ => "existing",
    };

    private readonly IAzureResourceDiscoveryService _discoveryService;
    private readonly IAppHostCodeGenerator _codeGenerator;
    private readonly IImportCommandPrompter _prompter;
    private readonly ResourceMappingService _mappingService;

    public ImportAzureCommand(
        IAzureResourceDiscoveryService discoveryService,
        IAppHostCodeGenerator codeGenerator,
        IImportCommandPrompter prompter,
        ResourceMappingService mappingService,
        IFeatures features,
        ICliUpdateNotifier updateNotifier,
        CliExecutionContext executionContext,
        IInteractionService interactionService,
        AspireCliTelemetry telemetry)
        : base("azure", ImportCommandStrings.AzureSubcommandDescription, features, updateNotifier, executionContext, interactionService, telemetry)
    {
        _discoveryService = discoveryService;
        _codeGenerator = codeGenerator;
        _prompter = prompter;
        _mappingService = mappingService;

        Options.Add(s_subscriptionOption);
        Options.Add(s_resourceGroupOption);
        Options.Add(s_outputOption);
        Options.Add(s_nameOption);
        Options.Add(s_modeOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var subscriptionId = parseResult.GetValue(s_subscriptionOption);
        var resourceGroupName = parseResult.GetValue(s_resourceGroupOption);
        var outputPath = parseResult.GetValue(s_outputOption) ?? Path.Combine(Environment.CurrentDirectory, "AppHost");
        var projectName = parseResult.GetValue(s_nameOption) ?? "AppHost";
        var mode = ParseImportMode(parseResult.GetValue(s_modeOption));

        try
        {
            InteractionService.DisplayMessage(KnownEmojis.Key, ImportCommandStrings.AuthenticatingWithAzure);

            if (string.IsNullOrEmpty(subscriptionId))
            {
                var subscriptions = await _discoveryService.GetSubscriptionsAsync(cancellationToken).ConfigureAwait(false);

                if (subscriptions.Count == 0)
                {
                    InteractionService.DisplayError(ImportCommandStrings.NoResourcesFound);
                    return ExitCodeConstants.InvalidCommand;
                }

                subscriptionId = await _prompter.PromptForSubscriptionAsync(subscriptions, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(resourceGroupName))
            {
                var resourceGroups = await _discoveryService.GetResourceGroupsAsync(subscriptionId, cancellationToken).ConfigureAwait(false);

                if (resourceGroups.Count == 0)
                {
                    InteractionService.DisplayError(ImportCommandStrings.NoResourcesFound);
                    return ExitCodeConstants.InvalidCommand;
                }

                resourceGroupName = await _prompter.PromptForResourceGroupAsync(resourceGroups, cancellationToken).ConfigureAwait(false);
            }

            InteractionService.DisplayMessage(KnownEmojis.MagnifyingGlassTiltedLeft, ImportCommandStrings.DiscoveringResources);

            var discovered = await _discoveryService.DiscoverResourcesAsync(subscriptionId, resourceGroupName, cancellationToken).ConfigureAwait(false);

            if (discovered.Count == 0)
            {
                InteractionService.DisplayMessage(KnownEmojis.Information, ImportCommandStrings.NoResourcesFound);
                return ExitCodeConstants.Success;
            }

            var mapped = _mappingService.MapAll(discovered);

            var selected = await _prompter.PromptForResourceSelectionAsync(
                ImportCommandStrings.SelectResources,
                mapped,
                FormatResourceForSelection,
                cancellationToken).ConfigureAwait(false);

            if (selected.Count == 0)
            {
                InteractionService.DisplayMessage(KnownEmojis.Information, ImportCommandStrings.NoResourcesFound);
                return ExitCodeConstants.Success;
            }

            if (!await _prompter.ConfirmImportAsync(selected.Count, outputPath, cancellationToken).ConfigureAwait(false))
            {
                return ExitCodeConstants.Success;
            }

            var sourceLabel = $"{subscriptionId}/{resourceGroupName}";
            GenerateProject(selected, sourceLabel, outputPath, projectName, mode);

            InteractionService.DisplaySuccess(ImportCommandStrings.ImportComplete);
            return ExitCodeConstants.Success;
        }
        catch (Exception ex) when (ex.GetType().FullName == "Azure.Identity.AuthenticationFailedException")
        {
            Telemetry.RecordError(ImportCommandStrings.AuthenticationFailed, ex);
            InteractionService.DisplayError(ImportCommandStrings.AuthenticationFailed);
            return ExitCodeConstants.InvalidCommand;
        }
        catch (Exception ex) when (ex.GetType().FullName == "Azure.RequestFailedException")
        {
            Telemetry.RecordError(ImportCommandStrings.ResourceDiscoveryFailed, ex);
            InteractionService.DisplayError(ImportCommandStrings.ResourceDiscoveryFailed);
            return ExitCodeConstants.InvalidCommand;
        }
    }

    private void GenerateProject(IReadOnlyList<ImportedResource> resources, string sourceLabel, string outputPath, string projectName, ImportMode mode)
    {
        InteractionService.DisplayMessage(KnownEmojis.Gear, ImportCommandStrings.GeneratingAppHost);

        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(outputPath, "Program.cs"), _codeGenerator.GenerateProgramCs(resources, sourceLabel, mode));
        File.WriteAllText(Path.Combine(outputPath, $"{projectName}.csproj"), _codeGenerator.GenerateCsproj(resources, projectName));

        ReportResourceNotes(resources);
    }

    private void ReportResourceNotes(IReadOnlyList<ImportedResource> resources)
    {
        foreach (var resource in resources)
        {
            switch (resource.SupportLevel)
            {
                case ImportSupportLevel.Placeholder:
                    InteractionService.DisplayMessage(KnownEmojis.Warning, $"{ImportCommandStrings.ComputeResourceDetected}: {resource.SourceResourceName}");
                    break;
                case ImportSupportLevel.Unsupported:
                    InteractionService.DisplayMessage(KnownEmojis.Information, $"{ImportCommandStrings.UnsupportedResourceSkipped}: {resource.SourceResourceName} ({resource.SourceType})");
                    break;
            }
        }
    }

    private static ImportMode ParseImportMode(string? value) =>
        string.Equals(value, "new", StringComparison.OrdinalIgnoreCase) ? ImportMode.New : ImportMode.Existing;

    private static string FormatResourceForSelection(ImportedResource resource)
    {
        var label = resource.SupportLevel switch
        {
            ImportSupportLevel.Supported => resource.FriendlyName ?? resource.SourceType,
            ImportSupportLevel.Placeholder => $"{resource.FriendlyName ?? resource.SourceType} (compute placeholder)",
            _ => $"{resource.SourceType} (unsupported)"
        };
        return $"{resource.SourceResourceName} [{label}]";
    }
}
