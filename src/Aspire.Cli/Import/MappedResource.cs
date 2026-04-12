// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Maps a source resource type to its corresponding Aspire builder method and NuGet package.
/// </summary>
internal sealed record MappedResource(
    string SourceType,
    string AspireBuilderMethod,
    string NuGetPackage,
    ResourceCategory Category,
    string FriendlyName,
    ImportSupportLevel SupportLevel);
