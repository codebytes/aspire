// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Parses infrastructure-as-code files in a directory and returns discovered resources.
/// </summary>
internal interface IIaCFileParser
{
    Task<IReadOnlyList<DiscoveredResource>> ParseAsync(string directoryPath, CancellationToken cancellationToken);
}
