// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Broad classification of a discovered infrastructure resource.
/// </summary>
internal enum ResourceCategory
{
    Database,
    Messaging,
    Storage,
    Cache,
    Compute,
    Networking,
    Management,
    Container,
    Orchestration,
    Other
}
