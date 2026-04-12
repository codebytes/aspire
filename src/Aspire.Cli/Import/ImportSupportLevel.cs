// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Indicates how well Aspire supports a discovered resource type.
/// </summary>
internal enum ImportSupportLevel
{
    /// <summary>
    /// Has an Aspire builder method and emits code.
    /// </summary>
    Supported,

    /// <summary>
    /// Compute or workload resource that emits a TODO comment.
    /// </summary>
    Placeholder,

    /// <summary>
    /// No Aspire equivalent; emits an informational comment.
    /// </summary>
    Unsupported
}
