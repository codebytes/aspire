// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Represents a resource discovered by parsing an infrastructure-as-code file.
/// </summary>
internal sealed record DiscoveredResource(
    string Name,
    string SourceType,
    string? Kind,
    string? Location,
    string SourceAddress,
    ResourceProvider Provider,
    IDictionary<string, string>? Tags);
