// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Aspire.Cli.Interaction;
using Aspire.Cli.Resources;

namespace Aspire.Cli.Import;

/// <summary>
/// Implements user prompts for the import command workflow using the interaction service.
/// </summary>
internal sealed class ImportCommandPrompter(IInteractionService interactionService) : IImportCommandPrompter
{
    public async Task<string> PromptForSubscriptionAsync(IReadOnlyList<(string Id, string Name)> subscriptions, CancellationToken ct)
    {
        var selected = await interactionService.PromptForSelectionAsync(
            ImportCommandStrings.SelectSubscription,
            subscriptions,
            s => $"{s.Name} ({s.Id})",
            ct).ConfigureAwait(false);

        return selected.Id;
    }

    public async Task<string> PromptForResourceGroupAsync(IReadOnlyList<(string Name, string Location)> resourceGroups, CancellationToken ct)
    {
        var selected = await interactionService.PromptForSelectionAsync(
            ImportCommandStrings.SelectResourceGroup,
            resourceGroups,
            rg => $"{rg.Name} ({rg.Location})",
            ct).ConfigureAwait(false);

        return selected.Name;
    }

    public async Task<IReadOnlyList<T>> PromptForResourceSelectionAsync<T>(string promptText, IReadOnlyList<T> resources, Func<T, string> formatter, CancellationToken ct) where T : notnull
    {
        return await interactionService.PromptForSelectionsAsync(
            promptText,
            resources,
            formatter,
            preSelected: resources,
            cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<bool> ConfirmImportAsync(int resourceCount, string outputPath, CancellationToken ct)
    {
        var message = string.Format(CultureInfo.CurrentCulture, ImportCommandStrings.ConfirmImport, resourceCount, outputPath);
        return await interactionService.ConfirmAsync(message, defaultValue: true, cancellationToken: ct).ConfigureAwait(false);
    }
}
