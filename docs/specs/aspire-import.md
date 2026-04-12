# PRD: `aspire import` — Infrastructure Import for Aspire

## Overview

`aspire import` reverse-engineers existing infrastructure definitions into Aspire AppHost projects. Teams with existing Azure deployments, Bicep templates, or Terraform configurations can adopt Aspire without manually recreating their infrastructure graph.

The command discovers resources from multiple sources, maps them to Aspire builder methods, and generates a complete AppHost project — including `Program.cs` with typed resource declarations and a `.csproj` with the correct NuGet package references.

## Problem Statement

Teams adopting Aspire today must manually:
1. Identify every infrastructure resource their application uses
2. Find the corresponding `Aspire.Hosting.Azure.*` NuGet package for each
3. Write `Add*()` calls for each resource in their AppHost
4. Wire resources together with `.WithReference()` calls
5. Decide whether to use `.AsExisting()` (reference deployed resources) or let Aspire provision

This is tedious, error-prone, and discourages adoption — especially for teams with large existing deployments or established IaC practices.

## Solution

A new `aspire import` CLI command family with pluggable source adapters:

```
aspire import azure      --subscription <id> --resource-group <name>
aspire import bicep       --path ./infra
aspire import terraform   --path ./infra
```

Each source adapter discovers resources, normalizes them into a common model, maps them to Aspire types, and feeds them into a shared code generator.

## Architecture

### Core Pipeline

```
┌─────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   Source     │     │  Discovery   │     │   Mapping    │     │     Code     │
│   Adapter    │────▶│   Result     │────▶│   Service    │────▶│   Generator  │
│  (parser)    │     │              │     │              │     │              │
└─────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
  Azure ARM SDK       DiscoveredResource   IResourceMapper      Program.cs
  Bicep CLI            (cloud-neutral)     (per-provider)       .csproj
  Terraform HCL                            registry pattern
  [future: K8s,
   Docker, AWS,
   GCP, Pulumi]
```

### Core Types

#### `DiscoveredResource` — Cloud-neutral discovery result

```csharp
record DiscoveredResource(
    string Name,              // resource name
    string SourceType,        // provider-native type (ARM type, azurerm_*, aws_*, k8s kind)
    string? Kind,             // provider-specific sub-type
    string? Location,         // nullable — not all providers have location
    string SourceAddress,     // stable IaC identity (TF: azurerm_cosmosdb_account.main)
    ResourceProvider Provider,// Azure, Aws, Gcp, Kubernetes, Docker, Unknown
    IDictionary<string, string>? Tags);
```

#### `ResourceProvider` — Closed enum, not stringly-typed

```csharp
enum ResourceProvider { Azure, Aws, Gcp, Kubernetes, Docker, Unknown }
```

#### `MappedResource` — Provider-to-Aspire mapping entry

```csharp
record MappedResource(
    string SourceType,             // what it maps from
    string AspireBuilderMethod,    // e.g. "AddAzureCosmosDB"
    string NuGetPackage,           // e.g. "Aspire.Hosting.Azure.CosmosDB"
    ResourceCategory Category,     // Database, Messaging, Storage, Cache, etc.
    string FriendlyName,           // "Azure Cosmos DB"
    ImportSupportLevel SupportLevel); // Supported, Placeholder, Unsupported
```

#### `ImportSupportLevel` — Separated from category

```csharp
enum ImportSupportLevel { Supported, Placeholder, Unsupported }
```

- **Supported**: Has a real Aspire builder method → emits `builder.Add*()` code
- **Placeholder**: Represents compute/workload → emits TODO comment suggesting `AddProject<>()`
- **Unsupported**: No Aspire equivalent → emits a descriptive comment

#### `ImportMode` — How resources are referenced

```csharp
enum ImportMode { Existing, New }
```

- **New** (default): Generates plain `Add*()` calls — Aspire manages provisioning
- **Existing**: Generates `.RunAsExisting(name, resourceGroup)` — resources must already be deployed

#### `IResourceMapper` — Provider-aware mapping (future)

```csharp
interface IResourceMapper
{
    ResourceProvider Provider { get; }
    bool TryMap(DiscoveredResource resource, out ImportedResource imported);
}
```

Injected as `IEnumerable<IResourceMapper>` into a `ResourceMappingService` that selects the correct mapper per resource based on `Provider` field. This supports mixed-provider scenarios (e.g., Terraform files with both `azurerm_*` and `aws_*` blocks).

### Resource Category Taxonomy

```csharp
enum ResourceCategory
{
    Database,      // CosmosDB, SQL, PostgreSQL, Kusto, DynamoDB, Cloud SQL
    Messaging,     // Service Bus, Event Hubs, SignalR, SQS, Pub/Sub
    Storage,       // Storage Accounts, S3, Cloud Storage
    Cache,         // Redis, ElastiCache, Memorystore
    Compute,       // Container Apps, App Service, Functions, ECS, Cloud Run
    Networking,    // VNet, ALB, Cloud Load Balancing
    Management,    // Key Vault, App Insights, Secrets Manager, Cloud KMS
    Container,     // Docker images, registries
    Orchestration, // Kubernetes, EKS, GKE, AKS
    Other
}
```

## Source Adapters

### Azure (Live Resource Group)

| Aspect | Detail |
|--------|--------|
| **Command** | `aspire import azure` |
| **Input** | Subscription ID + Resource Group name |
| **Mechanism** | Azure Resource Manager SDK (`ArmClient`) |
| **Auth** | `DefaultAzureCredential` |
| **Interactive** | Yes — prompts for subscription, RG, resource selection |
| **Feature flag** | `importAzureCommandEnabled` |
| **Prerequisite check** | Auth failure caught via exception handling |

### Bicep / ARM Templates

| Aspect | Detail |
|--------|--------|
| **Command** | `aspire import bicep --path ./infra` |
| **Input** | Directory containing `.bicep` files |
| **Mechanism** | Compiles via `bicep build --stdout` → parses ARM JSON |
| **Fallback** | `az bicep build --file <file> --stdout` |
| **Module support** | Recursively flattens `Microsoft.Resources/deployments` |
| **Feature flag** | `importBicepCommandEnabled` |
| **Prerequisite check** | Validates `bicep` or `az` CLI is installed |

#### ARM JSON Parsing Details

The compiled ARM JSON has a `resources` array. For each entry:
- Strip API version from type (e.g., `Microsoft.DocumentDB/databaseAccounts@2024-05-15` → `Microsoft.DocumentDB/databaseAccounts`)
- Extract `name`, `kind`, `location`, `tags`
- Recurse into nested `Microsoft.Resources/deployments` → `properties.template.resources`

### Terraform HCL

| Aspect | Detail |
|--------|--------|
| **Command** | `aspire import terraform --path ./infra` |
| **Input** | Directory containing `.tf` files |
| **Mechanism** | Line-by-line state machine parser for `resource` blocks |
| **Provider detection** | Resource prefix: `azurerm_` → Azure, `aws_` → AWS, `google_` → GCP |
| **Type normalization** | `TerraformToArmTypeMapping` (23 azurerm types mapped) |
| **Feature flag** | `importTerraformCommandEnabled` |
| **Prerequisite check** | Validates `.tf` files exist in directory |

#### Terraform Parser Details

State machine with `OutsideBlock` / `InResourceBlock` states:
- Matches: `resource "azurerm_cosmosdb_account" "main" {`
- Tracks brace depth to find block boundaries
- Extracts `name`, `location`, `kind`, `tags` attributes
- Handles literal values and variable references (falls back to logical name)
- Warns on `for_each`, `count`, `dynamic` blocks

### Future Adapters (Not Yet Implemented)

| Source | Command | Mechanism |
|--------|---------|-----------|
| **Kubernetes** | `aspire import kubernetes --context <ctx>` | `kubectl get` or kubeconfig parsing |
| **Docker Compose** | `aspire import docker --path ./docker-compose.yml` | YAML parsing |
| **AWS CloudFormation** | `aspire import cloudformation --path ./template.yaml` | YAML/JSON parsing |
| **AWS CDK** | `aspire import cdk --path ./cdk.out` | `cdk synth` → CloudFormation JSON |
| **Pulumi** | `aspire import pulumi --path ./Pulumi.yaml` | State file or `pulumi preview` |
| **GCP Deployment Manager** | `aspire import gcp --path ./config.yaml` | YAML parsing |

## Code Generation

### Generated `Program.cs`

Resources are grouped by category with section headers:

```csharp
// Auto-generated by 'aspire import' from 'contoso-rg' (mode: existing)
// Review and customize this file to match your application's needs.

var builder = DistributedApplication.CreateBuilder(args);

// ── Azure Cosmos DB ──────────────────────────────────────
var contosoCosmos = builder.AddAzureCosmosDB("contoso-cosmos")
    .AsExisting("contoso-cosmos", "contoso-rg");

// ── Azure Cache for Redis ────────────────────────────────
var contosoCache = builder.AddAzureRedis("contoso-cache")
    .AsExisting("contoso-cache", "contoso-rg");

// ── Compute Resources (TODO: Replace with local project references) ──
// Azure Container Apps: contoso-api
// TODO: Replace with builder.AddProject<Projects.ContosoApi>("contoso-api")
//       and add .WithReference() calls to connect to your resources.

// ── Unsupported Resources ────────────────────────────────
// The following resources were found but don't have direct Aspire equivalents:
// - contoso-vnet (Microsoft.Network/virtualNetworks)

builder.Build().Run();
```

With `--mode new`:
```csharp
var contosoCosmos = builder.AddAzureCosmosDB("contoso-cosmos");
var contosoCache = builder.AddAzureRedis("contoso-cache");
```

### Generated `.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" />
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsAspireHost>true</IsAspireHost>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
    <PackageReference Include="Aspire.Hosting.Azure.CosmosDB" />
    <PackageReference Include="Aspire.Hosting.Azure.Redis" />
  </ItemGroup>
</Project>
```

### Variable Name Sanitization

Azure resource names are converted to valid C# camelCase identifiers:
- `my-cosmos-db` → `myCosmosDb`
- `123storage` → `r123storage`
- `class` → `@class`
- Duplicate names get numeric suffixes: `myDb`, `myDb2`, `myDb3`

## Azure Resource Type Coverage

### Fully Supported (18 types → `ImportSupportLevel.Supported`)

| Category | ARM Type | Builder Method | Package |
|----------|----------|---------------|---------|
| Database | `Microsoft.DocumentDB/databaseAccounts` | `AddAzureCosmosDB` | `Aspire.Hosting.Azure.CosmosDB` |
| Database | `Microsoft.Sql/servers` | `AddAzureSqlServer` | `Aspire.Hosting.Azure.Sql` |
| Database | `Microsoft.DBforPostgreSQL/flexibleServers` | `AddAzurePostgresFlexibleServer` | `Aspire.Hosting.Azure.PostgreSQL` |
| Database | `Microsoft.Kusto/clusters` | `AddAzureKustoCluster` | `Aspire.Hosting.Azure.Kusto` |
| Messaging | `Microsoft.ServiceBus/namespaces` | `AddAzureServiceBus` | `Aspire.Hosting.Azure.ServiceBus` |
| Messaging | `Microsoft.EventHub/namespaces` | `AddAzureEventHubs` | `Aspire.Hosting.Azure.EventHubs` |
| Messaging | `Microsoft.SignalRService/signalR` | `AddAzureSignalR` | `Aspire.Hosting.Azure.SignalR` |
| Messaging | `Microsoft.SignalRService/webPubSub` | `AddAzureWebPubSub` | `Aspire.Hosting.Azure.WebPubSub` |
| Storage | `Microsoft.Storage/storageAccounts` | `AddAzureStorage` | `Aspire.Hosting.Azure.Storage` |
| Cache | `Microsoft.Cache/redis` | `AddAzureRedis` | `Aspire.Hosting.Azure.Redis` |
| Cache | `Microsoft.Cache/redisEnterprise` | `AddAzureManagedRedis` | `Aspire.Hosting.Azure.Redis` |
| Management | `Microsoft.KeyVault/vaults` | `AddAzureKeyVault` | `Aspire.Hosting.Azure.KeyVault` |
| Management | `Microsoft.Insights/components` | `AddAzureApplicationInsights` | `Aspire.Hosting.Azure.ApplicationInsights` |
| Management | `Microsoft.OperationalInsights/workspaces` | `AddAzureLogAnalyticsWorkspace` | `Aspire.Hosting.Azure.OperationalInsights` |
| Management | `Microsoft.AppConfiguration/configurationStores` | `AddAzureAppConfiguration` | `Aspire.Hosting.Azure.AppConfiguration` |
| Management | `Microsoft.Search/searchServices` | `AddAzureSearch` | `Aspire.Hosting.Azure.Search` |
| Management | `Microsoft.ContainerRegistry/registries` | `AddAzureContainerRegistry` | `Aspire.Hosting.Azure.ContainerRegistry` |
| Management | `Microsoft.CognitiveServices/accounts` | `AddAzureOpenAI` | `Aspire.Hosting.Azure.CognitiveServices` |

### Compute Placeholders (3 types → `ImportSupportLevel.Placeholder`)

| ARM Type | Generated Guidance |
|----------|-------------------|
| `Microsoft.App/containerApps` | `// TODO: Replace with builder.AddProject<Projects.X>()` |
| `Microsoft.Web/sites` | Same pattern |
| `Microsoft.Web/sites/functions` | Same pattern |

### Unsupported (everything else → `ImportSupportLevel.Unsupported`)

VNets, NSGs, managed identities, etc. appear as descriptive comments.

## Terraform-to-ARM Type Mapping (23 types)

Covers all `azurerm_*` providers that have ARM type equivalents in the mapping table above, plus compute types like `azurerm_container_app`, `azurerm_linux_web_app`, `azurerm_linux_function_app`, etc.

## Feature Flags

All import subcommands are gated behind individual feature flags (all default `false`):

```bash
aspire config set features.importAzureCommandEnabled true
aspire config set features.importBicepCommandEnabled true
aspire config set features.importTerraformCommandEnabled true
```

The parent `aspire import` command is visible when any child flag is enabled.

## CLI Options (Common to All Subcommands)

| Option | Description | Default |
|--------|-------------|---------|
| `--output, -o` | Output directory for generated project | `{cwd}/{name}.AppHost` |
| `--name, -n` | Project name | Derived from source |
| `--mode` | `new` (Aspire provisions) or `existing` (RunAsExisting) | `new` |

### Azure-specific Options

| Option | Description |
|--------|-------------|
| `--subscription` | Azure subscription ID (skips prompt) |
| `--resource-group` | Resource group name (skips prompt) |

### Bicep/Terraform-specific Options

| Option | Description |
|--------|-------------|
| `--path` | Path to directory containing IaC files (required) |

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Azure auth failure | Display "ensure you are logged in with az login" + telemetry |
| Bicep CLI not found | Display install URL + telemetry |
| No .tf files in path | Display clear message, return success |
| ARM JSON parse failure | Skip resource, log warning, continue |
| Unknown Terraform resource | Include as unsupported with warning |
| HCL dynamic/for_each | Log warning, skip complex constructs |

## Explicit Non-Goals for v1

1. **No automatic `.WithReference()` inference** — deployment `dependsOn` ≠ runtime connectivity. Generating incorrect references is worse than generating none.
2. **No AWS/GCP/Kubernetes adapters** — architecture supports them, but no Aspire hosting packages exist for these providers yet.
3. **No solution integration** — generated project is standalone; user adds it to their solution manually.
4. **No ServiceDefaults generation** — only generates AppHost, not the companion project.

## Future Work

| Feature | Description |
|---------|-------------|
| **Relationship inference** | Inspect app settings, connection strings, RBAC role assignments to generate `.WithReference()` |
| **Solution integration** | `--add-to-solution` flag to add generated project to existing `.sln` |
| **ServiceDefaults** | Generate companion ServiceDefaults project |
| **Docker Compose import** | Parse `docker-compose.yml` → `AddContainer()` calls |
| **Kubernetes import** | Parse manifests → container/service references |
| **AWS CloudFormation** | Parse stacks → resource mapping (when Aspire.Hosting.AWS.* exists) |
| **Pulumi state** | Import from Pulumi state files |
| **Interactive resource wiring** | Prompt user to connect resources with `.WithReference()` |
| **`terraform show` integration** | Use `terraform show -json` for richer metadata instead of HCL parsing |

## Test Coverage

| Test Area | Count | What's Covered |
|-----------|-------|----------------|
| ARM type mapping | 10 | All 21 types, case sensitivity, unknown types |
| Terraform type mapping | 6 | All 23 types, consistency with ARM mapping |
| Code generator | 20 | Both modes, all support levels, name sanitization, csproj dedup |
| Bicep parser | 15 | ARM JSON extraction, API version stripping, 3-level nesting, realistic multi-component apps |
| Terraform parser | 13 | Multi-file, comments, nested blocks, variable refs, all mapped types |
| Command integration | 7 | Help text, end-to-end with fakes, error paths |
| **Total** | **~71** | |
