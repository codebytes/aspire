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
/// Imports resources from Bicep files into an Aspire AppHost project.
/// </summary>
internal sealed class ImportBicepCommand : BaseCommand
{
    private static readonly Option<string> s_pathOption = new("--path")
    {
        Description = ImportCommandStrings.PathOption,
        Required = true,
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

    private readonly BicepFileParser _bicepParser;
    private readonly IAppHostCodeGenerator _codeGenerator;
    private readonly IImportCommandPrompter _prompter;
    private readonly ResourceMappingService _mappingService;

    public ImportBicepCommand(
        BicepFileParser bicepParser,
        IAppHostCodeGenerator codeGenerator,
        IImportCommandPrompter prompter,
        ResourceMappingService mappingService,
        IFeatures features,
        ICliUpdateNotifier updateNotifier,
        CliExecutionContext executionContext,
        IInteractionService interactionService,
        AspireCliTelemetry telemetry)
        : base("bicep", ImportCommandStrings.BicepSubcommandDescription, features, updateNotifier, executionContext, interactionService, telemetry)
    {
        _bicepParser = bicepParser;
        _codeGenerator = codeGenerator;
        _prompter = prompter;
        _mappingService = mappingService;

        Options.Add(s_pathOption);
        Options.Add(s_outputOption);
        Options.Add(s_nameOption);
        Options.Add(s_modeOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var bicepPath = parseResult.GetValue(s_pathOption)!;
        var outputPath = parseResult.GetValue(s_outputOption) ?? Path.Combine(Environment.CurrentDirectory, "AppHost");
        var projectName = parseResult.GetValue(s_nameOption) ?? "AppHost";
        var mode = ParseImportMode(parseResult.GetValue(s_modeOption));

        try
        {
            if (!CommandPathResolver.TryResolveCommand("bicep", out _, out _) &&
                !CommandPathResolver.TryResolveCommand("az", out _, out _))
            {
                InteractionService.DisplayError(ImportCommandStrings.BicepCliNotFound);
                return ExitCodeConstants.InvalidCommand;
            }

            var directoryPath = Path.GetFullPath(bicepPath);

            if (!Directory.Exists(directoryPath))
            {
                InteractionService.DisplayError(ImportCommandStrings.NoBicepFilesFound);
                return ExitCodeConstants.InvalidCommand;
            }

            InteractionService.DisplayMessage(KnownEmojis.Gear, ImportCommandStrings.CompilingBicep);

            var discovered = await _bicepParser.ParseAsync(directoryPath, cancellationToken).ConfigureAwait(false);

            if (discovered.Count == 0)
            {
                InteractionService.DisplayMessage(KnownEmojis.Information, ImportCommandStrings.NoIaCResourcesFound);
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

            GenerateProject(selected, directoryPath, outputPath, projectName, mode);

            InteractionService.DisplaySuccess(ImportCommandStrings.IaCImportComplete);
            return ExitCodeConstants.Success;
        }
        catch (InvalidOperationException ex)
        {
            Telemetry.RecordError(ImportCommandStrings.BicepCompileFailed, ex);
            InteractionService.DisplayError(ImportCommandStrings.BicepCompileFailed);
            return ExitCodeConstants.InvalidCommand;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Telemetry.RecordError(ImportCommandStrings.BicepImportFailed, ex);
            InteractionService.DisplayError(ImportCommandStrings.BicepImportFailed);
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
