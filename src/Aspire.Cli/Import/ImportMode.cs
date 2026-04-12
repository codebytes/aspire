// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Import;

/// <summary>
/// Controls whether imported resources reference existing infrastructure or are provisioned by Aspire.
/// </summary>
internal enum ImportMode
{
    /// <summary>
    /// Reference pre-deployed resources via <c>.AsExisting()</c>.
    /// </summary>
    Existing,

    /// <summary>
    /// Aspire manages provisioning via plain <c>Add*()</c> methods.
    /// </summary>
    New
}
