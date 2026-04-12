// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Provides user prompts for the import command workflow.
/// </summary>
internal interface IImportCommandPrompter
{
    Task<string> PromptForSubscriptionAsync(IReadOnlyList<(string Id, string Name)> subscriptions, CancellationToken ct);
    Task<string> PromptForResourceGroupAsync(IReadOnlyList<(string Name, string Location)> resourceGroups, CancellationToken ct);
    Task<IReadOnlyList<T>> PromptForResourceSelectionAsync<T>(string promptText, IReadOnlyList<T> resources, Func<T, string> formatter, CancellationToken ct) where T : notnull;
    Task<bool> ConfirmImportAsync(int resourceCount, string outputPath, CancellationToken ct);
}
