// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Aspire.Cli.Import;

/// <summary>
/// Parses Terraform <c>.tf</c> files in a directory and returns discovered resources.
/// </summary>
internal sealed class TerraformFileParser(ILogger<TerraformFileParser> logger) : IIaCFileParser
{
    private enum ParserState
    {
        OutsideBlock,
        InResourceBlock
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredResource>> ParseAsync(string directoryPath, CancellationToken cancellationToken)
    {
        var tfFiles = Directory.GetFiles(directoryPath, "*.tf", SearchOption.TopDirectoryOnly);

        if (tfFiles.Length == 0)
        {
            logger.LogDebug("No .tf files found in {Directory}.", directoryPath);
            return [];
        }

        var resources = new List<DiscoveredResource>();

        foreach (var filePath in tfFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var parsed = await ParseFileAsync(filePath, cancellationToken).ConfigureAwait(false);
                resources.AddRange(parsed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to read Terraform file {FilePath}. Skipping.", filePath);
            }
        }

        return resources;
    }

    private async Task<List<DiscoveredResource>> ParseFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var resources = new List<DiscoveredResource>();

        var state = ParserState.OutsideBlock;
        var braceDepth = 0;
        string? currentTerraformType = null;
        string? currentLogicalName = null;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? tags = null;
        var inTagsBlock = false;
        var tagsBraceDepth = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = lines[i];
            var trimmed = line.Trim();

            switch (state)
            {
                case ParserState.OutsideBlock:
                {
                    var match = TryMatchResourceDeclaration(trimmed);
                    if (match is not null)
                    {
                        state = ParserState.InResourceBlock;
                        braceDepth = CountBraceChange(line);
                        currentTerraformType = match.Value.TerraformType;
                        currentLogicalName = match.Value.LogicalName;
                        attributes.Clear();
                        tags = null;
                        inTagsBlock = false;

                        WarnOnMetaArguments(trimmed, filePath, i + 1);
                    }

                    break;
                }
                case ParserState.InResourceBlock:
                {
                    WarnOnMetaArguments(trimmed, filePath, i + 1);

                    if (inTagsBlock)
                    {
                        var tagChange = CountBraceChange(line);
                        tagsBraceDepth += tagChange;

                        if (tagsBraceDepth <= 0)
                        {
                            inTagsBlock = false;
                        }
                        else
                        {
                            var tagEntry = TryExtractAttribute(trimmed);
                            if (tagEntry is not null)
                            {
                                tags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                tags[tagEntry.Value.Key] = tagEntry.Value.Value;
                            }
                        }
                    }
                    else if (trimmed.StartsWith("tags", StringComparison.Ordinal) && trimmed.Contains('{'))
                    {
                        inTagsBlock = true;
                        tags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        tagsBraceDepth = CountBraceChange(line);
                        if (tagsBraceDepth <= 0)
                        {
                            inTagsBlock = false;
                        }
                    }
                    else
                    {
                        var attr = TryExtractAttribute(trimmed);
                        if (attr is not null)
                        {
                            attributes[attr.Value.Key] = attr.Value.Value;
                        }
                    }

                    braceDepth += CountBraceChange(line);

                    if (braceDepth <= 0)
                    {
                        if (currentTerraformType is not null && currentLogicalName is not null)
                        {
                            var resource = BuildResource(currentTerraformType, currentLogicalName, attributes, tags);
                            if (resource is not null)
                            {
                                resources.Add(resource);
                            }
                        }

                        state = ParserState.OutsideBlock;
                        currentTerraformType = null;
                        currentLogicalName = null;
                    }

                    break;
                }
            }
        }

        if (state == ParserState.InResourceBlock)
        {
            logger.LogWarning("Unterminated resource block in {FilePath}. Skipping.", filePath);
        }

        return resources;
    }

    private static (string TerraformType, string LogicalName)? TryMatchResourceDeclaration(string trimmedLine)
    {
        // Match: resource "provider_type" "logical_name" {
        if (!trimmedLine.StartsWith("resource ", StringComparison.Ordinal))
        {
            return null;
        }

        var firstQuote = trimmedLine.IndexOf('"');
        if (firstQuote < 0)
        {
            return null;
        }

        var secondQuote = trimmedLine.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
        {
            return null;
        }

        var terraformType = trimmedLine[(firstQuote + 1)..secondQuote];

        var thirdQuote = trimmedLine.IndexOf('"', secondQuote + 1);
        if (thirdQuote < 0)
        {
            return null;
        }

        var fourthQuote = trimmedLine.IndexOf('"', thirdQuote + 1);
        if (fourthQuote < 0)
        {
            return null;
        }

        var logicalName = trimmedLine[(thirdQuote + 1)..fourthQuote];

        if (string.IsNullOrWhiteSpace(terraformType) || string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        return (terraformType, logicalName);
    }

    private static (string Key, string Value)? TryExtractAttribute(string trimmedLine)
    {
        // Match: key = "value" or key = value
        var equalsIndex = trimmedLine.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return null;
        }

        // Skip lines that look like blocks (e.g., "lifecycle {")
        var key = trimmedLine[..equalsIndex].Trim();
        if (key.Contains(' ') || key.Contains('{') || key.Contains('}'))
        {
            return null;
        }

        var valueRaw = trimmedLine[(equalsIndex + 1)..].Trim();

        // Extract literal string value between quotes.
        if (valueRaw.StartsWith('"') && valueRaw.IndexOf('"', 1) is var closingQuote and > 0)
        {
            return (key, valueRaw[1..closingQuote]);
        }

        // Non-literal (variable reference, expression, etc.) — skip.
        return null;
    }

    private static DiscoveredResource? BuildResource(
        string terraformType,
        string logicalName,
        Dictionary<string, string> attributes,
        Dictionary<string, string>? tags)
    {
        var provider = ResolveProvider(terraformType);
        var sourceAddress = $"{terraformType}.{logicalName}";

        string sourceType;
        if (provider == ResourceProvider.Azure && TerraformToArmTypeMapping.TryGetArmType(terraformType, out var armType))
        {
            sourceType = armType!;
        }
        else
        {
            sourceType = terraformType;
        }

        attributes.TryGetValue("name", out var name);
        name ??= logicalName;

        attributes.TryGetValue("location", out var location);
        attributes.TryGetValue("kind", out var kind);

        return new DiscoveredResource(
            Name: name,
            SourceType: sourceType,
            Kind: kind,
            Location: location,
            SourceAddress: sourceAddress,
            Provider: provider,
            Tags: tags);
    }

    private static ResourceProvider ResolveProvider(string terraformType)
    {
        return terraformType switch
        {
            _ when terraformType.StartsWith("azurerm_", StringComparison.Ordinal) => ResourceProvider.Azure,
            _ when terraformType.StartsWith("aws_", StringComparison.Ordinal) => ResourceProvider.Aws,
            _ when terraformType.StartsWith("google_", StringComparison.Ordinal) => ResourceProvider.Gcp,
            _ when terraformType.StartsWith("kubernetes_", StringComparison.Ordinal) => ResourceProvider.Kubernetes,
            _ when terraformType.StartsWith("docker_", StringComparison.Ordinal) => ResourceProvider.Docker,
            _ => ResourceProvider.Unknown
        };
    }

    private static int CountBraceChange(string line)
    {
        var depth = 0;
        var inString = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '#')
            {
                break;
            }

            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                break;
            }

            switch (c)
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
            }
        }

        return depth;
    }

    private void WarnOnMetaArguments(string trimmedLine, string filePath, int lineNumber)
    {
        if (trimmedLine.StartsWith("for_each", StringComparison.Ordinal))
        {
            logger.LogWarning("for_each detected at {FilePath}:{Line}. Expanded instances are not enumerated.", filePath, lineNumber);
        }
        else if (trimmedLine.StartsWith("count", StringComparison.Ordinal))
        {
            logger.LogWarning("count detected at {FilePath}:{Line}. Expanded instances are not enumerated.", filePath, lineNumber);
        }
        else if (trimmedLine.StartsWith("dynamic", StringComparison.Ordinal))
        {
            logger.LogWarning("dynamic block detected at {FilePath}:{Line}. Dynamic content is not expanded.", filePath, lineNumber);
        }
    }
}
