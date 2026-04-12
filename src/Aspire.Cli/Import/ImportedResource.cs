// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Represents a resource that has been mapped and is ready for code generation.
/// </summary>
internal sealed record ImportedResource(
    string Name,
    string SourceResourceName,
    string SourceType,
    string? Kind,
    ResourceCategory Category,
    string? AspireBuilderMethod,
    string? NuGetPackage,
    string? FriendlyName,
    ImportSupportLevel SupportLevel);
