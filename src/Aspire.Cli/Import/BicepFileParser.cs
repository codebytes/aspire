// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Aspire.Cli.DotNet;
using Aspire.Cli.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Cli.Import;

/// <summary>
/// Parses Bicep and ARM JSON files to discover Azure resources.
/// </summary>
internal sealed class BicepFileParser(
    IProcessExecutionFactory processExecutionFactory,
    ILogger<BicepFileParser> logger) : IIaCFileParser
{
    public async Task<IReadOnlyList<DiscoveredResource>> ParseAsync(string directoryPath, CancellationToken cancellationToken)
    {
        var bicepFile = FindEntryPointBicepFile(directoryPath);
        if (bicepFile is null)
        {
            return [];
        }

        var armJson = await CompileBicepToArmAsync(bicepFile, cancellationToken).ConfigureAwait(false);
        if (armJson is null)
        {
            return [];
        }

        return ParseArmJsonResources(armJson);
    }

    internal string? FindEntryPointBicepFile(string directoryPath)
    {
        var mainBicep = Path.Combine(directoryPath, "main.bicep");
        if (File.Exists(mainBicep))
        {
            logger.LogDebug("Found entry point Bicep file: {BicepFile}", mainBicep);
            return mainBicep;
        }

        var bicepFiles = Directory.GetFiles(directoryPath, "*.bicep", SearchOption.TopDirectoryOnly);

        if (bicepFiles.Length == 0)
        {
            logger.LogWarning("No Bicep files found in {DirectoryPath}.", directoryPath);
            return null;
        }

        if (bicepFiles.Length == 1)
        {
            logger.LogDebug("Found single Bicep file: {BicepFile}", bicepFiles[0]);
            return bicepFiles[0];
        }

        throw new InvalidOperationException(
            $"Multiple Bicep files found in '{directoryPath}' and no main.bicep exists. " +
            "Either rename your entry point to 'main.bicep' or specify the file to compile.");
    }

    internal async Task<string?> CompileBicepToArmAsync(string bicepFile, CancellationToken cancellationToken)
    {
        // Try the standalone Bicep CLI first.
        if (CommandPathResolver.TryResolveCommand("bicep", out var bicepPath, out _))
        {
            var result = await RunProcessAsync(
                bicepPath!,
                ["build", bicepFile, "--stdout"],
                Path.GetDirectoryName(bicepFile)!,
                cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                return result.StandardOutput;
            }

            logger.LogWarning(
                "Bicep CLI exited with code {ExitCode}. Stderr: {Stderr}",
                result.ExitCode,
                result.StandardError);
        }

        // Fall back to the Azure CLI's built-in Bicep.
        if (CommandPathResolver.TryResolveCommand("az", out var azPath, out _))
        {
            var result = await RunProcessAsync(
                azPath!,
                ["bicep", "build", "--file", bicepFile, "--stdout"],
                Path.GetDirectoryName(bicepFile)!,
                cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                return result.StandardOutput;
            }

            logger.LogWarning(
                "Azure CLI bicep build exited with code {ExitCode}. Stderr: {Stderr}",
                result.ExitCode,
                result.StandardError);
        }

        throw new InvalidOperationException(
            "Neither the Bicep CLI ('bicep') nor the Azure CLI ('az') was found on PATH. " +
            "Install the Bicep CLI (https://learn.microsoft.com/azure/azure-resource-manager/bicep/install) " +
            "or the Azure CLI (https://learn.microsoft.com/cli/azure/install-azure-cli) and try again.");
    }

    internal static List<DiscoveredResource> ParseArmJsonResources(string armJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(armJson);
        }
        catch (JsonException)
        {
            return [];
        }

        using (doc)
        {
            var resources = new List<DiscoveredResource>();

            if (doc.RootElement.TryGetProperty("resources", out var resourcesElement) &&
                resourcesElement.ValueKind == JsonValueKind.Array)
            {
                ExtractResources(resourcesElement, resources);
            }

            return resources;
        }
    }

    private static void ExtractResources(JsonElement resourcesArray, List<DiscoveredResource> results)
    {
        foreach (var resource in resourcesArray.EnumerateArray())
        {
            if (!resource.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var rawType = typeElement.GetString()!;

            // Strip the @apiVersion suffix if present (e.g. "Microsoft.Web/sites@2022-03-01").
            var atIndex = rawType.IndexOf('@');
            var resourceType = atIndex >= 0 ? rawType[..atIndex] : rawType;

            var name = resource.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()!
                : resourceType;

            var kind = resource.TryGetProperty("kind", out var kindElement) && kindElement.ValueKind == JsonValueKind.String
                ? kindElement.GetString()
                : null;

            var location = resource.TryGetProperty("location", out var locationElement) && locationElement.ValueKind == JsonValueKind.String
                ? locationElement.GetString()
                : null;

            var tags = ExtractTags(resource);

            var sourceAddress = $"/subscriptions/imported/resourceGroups/bicep/providers/{resourceType}/{name}";

            // For nested deployments, recurse into the template but don't add the deployment wrapper itself.
            if (string.Equals(resourceType, "Microsoft.Resources/deployments", StringComparison.OrdinalIgnoreCase))
            {
                if (resource.TryGetProperty("properties", out var propsElement) &&
                    propsElement.TryGetProperty("template", out var templateElement) &&
                    templateElement.TryGetProperty("resources", out var nestedResources) &&
                    nestedResources.ValueKind == JsonValueKind.Array)
                {
                    ExtractResources(nestedResources, results);
                }

                continue;
            }

            results.Add(new DiscoveredResource(
                Name: name,
                SourceType: resourceType,
                Kind: kind,
                Location: location,
                SourceAddress: sourceAddress,
                Provider: ResourceProvider.Azure,
                Tags: tags));
        }
    }

    private static Dictionary<string, string>? ExtractTags(JsonElement resource)
    {
        if (!resource.TryGetProperty("tags", out var tagsElement) ||
            tagsElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var tags = new Dictionary<string, string>();
        foreach (var tag in tagsElement.EnumerateObject())
        {
            if (tag.Value.ValueKind == JsonValueKind.String)
            {
                tags[tag.Name] = tag.Value.GetString()!;
            }
        }

        return tags.Count > 0 ? tags : null;
    }

    private async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessAsync(
        string fileName,
        string[] args,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        var options = new ProcessInvocationOptions
        {
            StandardOutputCallback = line => stdoutBuilder.AppendLine(line),
            StandardErrorCallback = line => stderrBuilder.AppendLine(line),
            SuppressLogging = true
        };

        using var execution = processExecutionFactory.CreateExecution(
            fileName,
            args,
            env: null,
            new DirectoryInfo(workingDirectory),
            options);

        if (!execution.Start())
        {
            return (-1, string.Empty, $"Failed to start process: {fileName}");
        }

        var exitCode = await execution.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (exitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }
}
