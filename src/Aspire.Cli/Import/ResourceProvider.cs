// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Identifies the cloud or platform provider of a discovered resource.
/// </summary>
internal enum ResourceProvider
{
    Azure,
    Aws,
    Gcp,
    Kubernetes,
    Docker,
    Unknown
}
