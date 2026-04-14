// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using Aspire.Cli.Configuration;
using Aspire.Cli.Interaction;
using Aspire.Cli.Resources;
using Aspire.Cli.Telemetry;
using Aspire.Cli.Utils;

namespace Aspire.Cli.Commands;

/// <summary>
/// Imports existing Azure resources, Bicep files, or Terraform files into an Aspire AppHost project.
/// </summary>
internal sealed class ImportCommand : BaseCommand
{
    internal override HelpGroup HelpGroup => HelpGroup.AppCommands;

    public ImportCommand(
        ImportAzureCommand azureCommand,
        ImportBicepCommand bicepCommand,
        ImportTerraformCommand terraformCommand,
        IFeatures features,
        ICliUpdateNotifier updateNotifier,
        CliExecutionContext executionContext,
        IInteractionService interactionService,
        AspireCliTelemetry telemetry)
        : base("import", ImportCommandStrings.Description, features, updateNotifier, executionContext, interactionService, telemetry)
    {
        if (features.IsFeatureEnabled(KnownFeatures.ImportAzureCommandEnabled, false))
        {
            Subcommands.Add(azureCommand);
        }

        if (features.IsFeatureEnabled(KnownFeatures.ImportBicepCommandEnabled, false))
        {
            Subcommands.Add(bicepCommand);
        }

        if (features.IsFeatureEnabled(KnownFeatures.ImportTerraformCommandEnabled, false))
        {
            Subcommands.Add(terraformCommand);
        }

        if (Subcommands.Count == 0)
        {
            Hidden = true;
        }
    }

    protected override Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        new HelpAction().Invoke(parseResult);
        return Task.FromResult(ExitCodeConstants.InvalidCommand);
    }
}
