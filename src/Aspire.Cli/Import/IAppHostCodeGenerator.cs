// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Generates AppHost project files from imported infrastructure resources.
/// </summary>
internal interface IAppHostCodeGenerator
{
    /// <summary>
    /// Generates the Program.cs content for an Aspire AppHost project.
    /// </summary>
    /// <param name="resources">The imported resources to generate code for.</param>
    /// <param name="sourceLabel">A label identifying the import source (e.g., file path or environment name).</param>
    /// <param name="mode">Controls whether resources reference existing infrastructure or are provisioned by Aspire.</param>
    /// <param name="resourceGroup">The Azure resource group name for existing resources. When <c>null</c>, the generated code passes <c>null</c> to use the current resource group.</param>
    /// <returns>The generated Program.cs file content.</returns>
    string GenerateProgramCs(IReadOnlyList<ImportedResource> resources, string sourceLabel, ImportMode mode = ImportMode.New, string? resourceGroup = null);

    /// <summary>
    /// Generates the .csproj content for an Aspire AppHost project.
    /// </summary>
    /// <param name="resources">The imported resources to include package references for.</param>
    /// <param name="projectName">The name of the AppHost project.</param>
    /// <returns>The generated .csproj file content.</returns>
    string GenerateCsproj(IReadOnlyList<ImportedResource> resources, string projectName);
}
