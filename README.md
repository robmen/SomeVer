# SomeVer

![SomeVer](https://github.com/robmen/SomeVer/blob/main/SomeVer/SomeVer.png)

_[![SomeVer NuGet version](https://img.shields.io/nuget/v/SomeVer.svg?style=flat&label=nuget%3A%20SomeVer)](https://www.nuget.org/packages/SomeVer)_
_[![Build status](https://github.com/robmen/somever/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/robmen/somever/actions/workflows/build.yml)_

SomeVer is an [MSBuild SDK and build-only NuGet package](https://www.nuget.org/packages/SomeVer) built on the version-calculation model from [MinVer](https://github.com/adamralph/minver). It brings MinVer's tag-first behavior to both ordinary SDK-style projects and larger build systems, where you can:

- keep `Microsoft.NET.Sdk` and add one `PackageReference` to a compiling project
- use a version-only/no-targets MSBuild project such as `*.verproj`
- generate versioned text files from templates for non-.NET outputs
- expose more build metadata such as commit SHA and full height

## Contents

- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [How to use SomeVer](#how-to-use-somever)
- [How version calculation works](#how-version-calculation-works)
- [SDK and project types](#sdk-and-project-types)
- [MSBuild outputs](#msbuild-outputs)
- [Version templates](#version-templates)
- [Configuration](#configuration)
- [Targets and build order](#targets-and-build-order)
- [Large-repository pattern](#large-repository-pattern)
- [CI guidance](#ci-guidance)
- [Troubleshooting](#troubleshooting)

## Prerequisites

- [.NET SDK 10.0 or later](https://dot.net/)
- [Git](https://git-scm.com/)

The package contains the SomeVer task for `net10.0` MSBuild and for .NET Framework MSBuild (`net472`). The Git repository and its tags must be available to the build.

## Quick start

For an ordinary compiling project, keep `Microsoft.NET.Sdk` and add SomeVer as a build-only package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <SomeVerTagPrefix>v</SomeVerTagPrefix>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SomeVer" Version="x.y.z" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Use the same `PackageReference` without `Version` when Central Package Management supplies it in `Directory.Packages.props`. SomeVer calculates assembly, file, informational, and package versions from the consuming repository's Git history. It adds build targets only; it does not add a runtime library reference.

For a version-only or no-targets project, use SomeVer as the SDK. The SDK version should be the SomeVer package version you want to use:

```xml
<Project Sdk="SomeVer/0.1.1">
  <PropertyGroup>
    <SomeVerTagPrefix>v</SomeVerTagPrefix>
  </PropertyGroup>
</Project>
```

Build the project from a Git repository:

```shell
dotnet build
```

Create a release by tagging the commit you want to ship:

```shell
git tag v1.2.3
git push --tags
```

Build that tagged commit and SomeVer will set the version properties from the tag.

If you prefer to centralize SDK versions, use `global.json`:

```json
{
  "msbuild-sdks": {
    "SomeVer": "0.1.1"
  }
}
```

Then your projects can use:

```xml
<Project Sdk="SomeVer" />
```

## How to use SomeVer

Use `PackageReference` for a normal application or library that should keep `Microsoft.NET.Sdk`. Use the SDK workflow below for a dedicated version project, a no-targets orchestration project, or a repository that uses version templates to share calculated values with multiple products.

SomeVer follows MinVer's tag-first model:

1. Put your code in Git.
2. Tag the commit you want to release with a valid SemVer 2.0 version.
3. Build from that commit.

For example, if you use a `v` prefix:

```shell
git tag v2.4.0
git push --tags
```

Configure the matching prefix in your project:

```xml
<PropertyGroup>
  <SomeVerTagPrefix>v</SomeVerTagPrefix>
</PropertyGroup>
```

SomeVer imports Microsoft.NET.Sdk infrastructure, but its SDK is designed primarily for version/bootstrap projects that do not compile an assembly. A normal SDK-style project can consume the generated props from a SomeVer project. If you use `SomeVer` as the SDK of a compiling project, review its no-targets defaults and explicitly opt into the compile behavior and items your project needs.

For build-orchestration or no-targets projects, `SomeVer` can be the only SDK. That allows the same repository version to drive generated files, packaging, installers, or other non-assembly outputs.

## SDK and project types

For a version-only project, a target framework is not normally needed. Some build systems include one to make restore behavior explicit:

```xml
<Project Sdk="SomeVer/0.1.1">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <SomeVerTagPrefix>v</SomeVerTagPrefix>
  </PropertyGroup>
</Project>
```

The SDK supplies no-targets defaults such as disabled default compile items and no assembly output. It is therefore a good fit for a project whose job is to calculate a version and write files, but it is not a drop-in replacement for the normal compiling SDK behavior without reviewing those defaults.

These advanced hooks let a repository add imports around the SDK. Each value is an MSBuild file path; an empty or nonexistent path is ignored.

| Property | Import location |
| --- | --- |
| `CustomBeforeSomeVerSdkProps` | `Sdk.props`, before SomeVer props |
| `CustomAfterSomeVerSdkProps` | `Sdk.props`, after SomeVer props |
| `CustomBeforeSomeVerSdk` | `Sdk.targets`, before the SDK targets |
| `CustomAfterSomeVerSdk` | `Sdk.targets`, before SomeVer's targets |
| `SomeVerTasksPath` | Overrides the packaged `SomeVerTasks.dll` path |

## How version calculation works

The calculation model is inherited from MinVer, with SomeVer-specific outputs layered on top.

- SomeVer considers reachable tags that match `SomeVerTagPrefix` and contain valid SemVer 2.0 after the prefix is removed. Tag matching is case-insensitive.
- Non-version tags are ignored. The highest reachable semantic version wins; ties follow the order in which the history is traversed.
- If the selected version is a pre-release, that version is reused and height is appended.
- If the selected version is RTM, the configured part is auto-incremented and the default pre-release identifiers are added.
- If no matching tag is found, the default version is `0.0.0-alpha.0` and height is measured from the root commit.

Height is the number of commits since the selected version candidate. For example, after `v1.2.3`, two more commits produce `1.2.4-alpha.0.2` by default.

SomeVer also calculates `SomeVerFullHeight`, which is the number of commits back to the nearest reachable RTM tag, or to the root commit if no RTM tag exists. That value is used for `FileVersion`.

## MSBuild outputs

SomeVer sets these custom MSBuild properties:

- `SomeVerVersion`
- `SomeVerMajor`
- `SomeVerMinor`
- `SomeVerPatch`
- `SomeVerLabel`
- `SomeVerHeight`
- `SomeVerFullHeight`
- `SomeVerSha`
- `SomeVerShortSha`
- `SomeVerNextMajor`
- `SomeVerNextMinor`
- `SomeVerDashLabel`

It then applies them to the standard SDK version properties:

| Property | Value |
| --- | --- |
| `AssemblyVersion` | `{SomeVerMajor}.0.0.0` |
| `FileVersion` | `{SomeVerMajor}.{SomeVerMinor}.{SomeVerPatch}.{SomeVerFullHeight}` |
| `InformationalVersion` | `{SomeVerVersion}` |
| `PackageVersion` | `{SomeVerMajor}.{SomeVerMinor}.{SomeVerPatch}` or `{SomeVerMajor}.{SomeVerMinor}.{SomeVerPatch}-{SomeVerLabel}` |
| `Version` | same as `PackageVersion` |

This differs from MinVer in two important ways:

- `FileVersion` uses `SomeVerFullHeight` as its fourth component.
- `SomeVer` exposes commit SHA values and the pre-release label directly for other build steps.

`SomeVerSha` and `SomeVerShortSha` identify the selected version candidate's commit; they are not necessarily the SHA of `HEAD`. `SomeVerHeight` is still reported when `SomeVerIgnoreHeight` is enabled, even though it is not added to the version string.

`PackageVersion` and `Version` intentionally omit build metadata. `InformationalVersion` retains it. `SomeVerNextMajor`, `SomeVerNextMinor`, and `SomeVerDashLabel` are calculated after the version task and are available to templates and later targets.

## Version templates

SomeVer can generate files from templates during the build. This is useful when you need the calculated version in installer source, manifests, scripts, or other non-.NET assets.

Add `VersionTemplate` items to your project:

```xml
<Project Sdk="SomeVer/x.y.z">
  <ItemGroup>
    <VersionTemplate Include="version.txt.template"
                     OutputPath="$(IntermediateOutputPath)version.txt" />
  </ItemGroup>
</Project>
```

`Include` is the input template and `OutputPath` is required item metadata.

Example template file:

```text
Version={informationalversion}
PackageVersion={packageversion}
FileVersion={fileversion}
Sha={sha}
ShortSha={shortsha}
```

Supported template tokens are lowercase and case-sensitive:

| Token | Replacement |
| --- | --- |
| `{fileversion}` | `$(FileVersion)` |
| `{assemblyversion}` | `$(AssemblyVersion)` |
| `{informationalversion}` | `$(InformationalVersion)` |
| `{packageversion}` | `$(PackageVersion)` |
| `{nextmajor}` | `$(SomeVerNextMajor)` |
| `{nextminor}` | `$(SomeVerNextMinor)` |
| `{major}` | `$(SomeVerMajor)` |
| `{minor}` | `$(SomeVerMinor)` |
| `{patch}` | `$(SomeVerPatch)` |
| `{label}` | `$(SomeVerLabel)` |
| `{dashlabel}` | `$(SomeVerDashLabel)` |
| `{height}` | `$(SomeVerHeight)` |
| `{fullheight}` | `$(SomeVerFullHeight)` |
| `{sha}` | `$(SomeVerSha)` |
| `{shortsha}` | `$(SomeVerShortSha)` |

Every occurrence is replaced. Unknown tokens and ordinary text are left unchanged. `{label}` has no leading dash; `{dashlabel}` is `-` plus the label, or empty for an RTM version. There is no `{version}` token: use `{informationalversion}` for the complete SemVer or `{packageversion}` for the NuGet-safe version.

The generated file is not automatically imported or included. Add the generated props, source, resource, JSON, or package file to the consuming build as appropriate. The target reads and writes text, so the replacement system works for XML, C#, C/C++ resource scripts, JSON, and plain text.

For example, a generated props file can give the values repository-specific names:

```xml
<Project>
  <PropertyGroup>
    <MyVerInfoImported>true</MyVerInfoImported>
    <MyVerInfoMajor>{major}</MyVerInfoMajor>
    <MyVerInfoMinor>{minor}</MyVerInfoMinor>
    <MyVerInfoPatch>{patch}</MyVerInfoPatch>
    <MyVerInfoHeight>{height}</MyVerInfoHeight>
    <MyVerInfoFullHeight>{fullheight}</MyVerInfoFullHeight>
    <MyVerInfoLabel>{label}</MyVerInfoLabel>
    <MyVerInfoDashLabel>{dashlabel}</MyVerInfoDashLabel>
    <MyVerInfoSha>{sha}</MyVerInfoSha>
    <MyVerInfoShortSha>{shortsha}</MyVerInfoShortSha>
    <MyVerInfoVersion>{informationalversion}</MyVerInfoVersion>
  </PropertyGroup>
</Project>
```

## Configuration

Configure SomeVer with MSBuild properties:

SomeVer is an MSBuild SDK/task. Configure it with MSBuild properties in the project, imported files, or `-p:` command-line switches; the package does not provide MinVer's separate CLI option or environment-variable interface.

| Property | Default | Purpose |
| --- | --- | --- |
| `SomeVerTagPrefix` | empty | Matches version tags with a prefix such as `v`, `foo-v`, or `main-`. |
| `SomeVerMinimumMajorMinor` | `0.0` | Forces a minimum `major.minor` for interim builds. |
| `SomeVerAutoIncrement` | `patch` | Chooses which part to increment after an RTM tag: `patch`, `minor`, or `major`. |
| `SomeVerDefaultPreReleaseIdentifiers` | `alpha.0` | Replaces the default pre-release identifiers; `dev` and `build` are common choices. |
| `SomeVerIgnoreHeight` | `false` | Prevents appending height or auto-incrementing the calculated version. |
| `SomeVerBuildMetadata` | empty | Appends SemVer build metadata to `SomeVerVersion` and `InformationalVersion`. |
| `SomeVerIncludeDefaultPreReleaseIdentifiersWithPrereleases` | `false` | Appends the default identifiers even when the source tag is already a pre-release. |
| `SomeVerVerbosity` | normal | `detailed`/`d` or `diagnostic`/`diag` promotes input/output messages. |
| `SomeVerSkip` | empty/false | Disables SomeVer calculation and template generation when `true`. |
| `SomeVerReportBuildNumber` | empty/false | Emits an Azure Pipelines command setting the build number to `SomeVerVersion` when `true`. |

Example:

```xml
<PropertyGroup>
  <SomeVerTagPrefix>v</SomeVerTagPrefix>
  <SomeVerDefaultPreReleaseIdentifiers>dev</SomeVerDefaultPreReleaseIdentifiers>
  <SomeVerIncludeDefaultPreReleaseIdentifiersWithPrereleases>true</SomeVerIncludeDefaultPreReleaseIdentifiersWithPrereleases>
  <SomeVerBuildMetadata>build.42</SomeVerBuildMetadata>
</PropertyGroup>
```

With that configuration:

- `v1.2.3` plus two commits becomes `1.2.4-dev.2`
- `v1.2.3-rc.1` plus one commit becomes `1.2.3-rc.1.dev.1`

`SomeVerDefaultPreReleaseIdentifiers` is passed to the MSBuild task as a string array. A simple value such as `dev` is the usual configuration. If multiple array entries are required, use MSBuild's semicolon-separated syntax, for example `preview;0`.

The target also logs `SomeVerDefaultPreReleasePhase` and `SomeVerVersionOverride` for compatibility with MinVer-era diagnostics, but the current SomeVer task does not consume either property. They do not alter the calculated version.

`SomeVerDetailed`, `SomeVerSdkPropsImported`, `SomeVerPropsImported`, and `SomeVerTargetsImported` are implementation/import markers. `SomeVerInfoImported` is a useful convention for a generated version-info props file in a larger repository; it is not an input to the SomeVer calculation.

## Targets and build order

The SDK imports these targets:

| Target | Behavior |
| --- | --- |
| `SomeVer` | Calculates the version, sets all `SomeVer*` outputs, and assigns the standard version properties. It skips design-time builds and `SomeVerSkip=true`. |
| `SomeVerApplyVersionTemplates` | Applies every `@(VersionTemplate)` item, using the item identity as input and `%(OutputPath)` as output. |
| `SomeVerBuild` | Hook scheduled before `BeforeCompile`, `GetAssemblyVersion`, and `CoreCompile`; depends on `SomeVer` and `SomeVerApplyVersionTemplates`. |
| `SomeVerClean` | Packaging/clean-up hook before `Clean` when `GeneratePackageOnBuild=true`; it depends on a consumer-provided `SomeVerInfo` target. |
| `SomeVerReportBuildNumber` | Runs after `Build` when `SomeVerReportBuildNumber=true` and emits `##vso[build.updatebuildnumber]...`. |

Normally invoke `Build`. A custom orchestration target that needs the calculated values should depend on `SomeVer`; one that needs generated files should depend on both targets:

```xml
<Target Name="GenerateVersionFiles"
        DependsOnTargets="SomeVer;SomeVerApplyVersionTemplates" />
```

For custom Azure Pipelines policies, use a normal target and the already-calculated `$(InformationalVersion)`:

```xml
<Target Name="ReportBuildNumber"
        AfterTargets="Build"
        Condition="'$(TF_BUILD)' != '' and '$(OfficialBuild)' != ''">
  <Message Importance="high" Text="Set build number: $(InformationalVersion)" />
  <Message Importance="high" Text="##vso[build.updatebuildnumber]$(InformationalVersion)" />
</Target>
```

## Large-repository pattern

The pattern used by the WiX build systems is:

1. Create one small SomeVer version project.
2. Configure the tag prefix and pre-release policy in that project.
3. Add one `VersionTemplate` item for each generated artifact.
4. Write the outputs to a shared build/generated-files directory.
5. Import the generated props in the rest of the repository.

One version project can generate props, a C# version-info file, a Windows `.rc` file, and package-version files:

```xml
<Project Sdk="SomeVer/0.1.1">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <SomeVerTagPrefix>v</SomeVerTagPrefix>
    <SomeVerAutoIncrement>major</SomeVerAutoIncrement>
    <SomeVerDefaultPreReleaseIdentifiers>build</SomeVerDefaultPreReleaseIdentifiers>
    <SomeVerIncludeDefaultPreReleaseIdentifiersWithPrereleases>true</SomeVerIncludeDefaultPreReleaseIdentifiersWithPrereleases>
    <SomeVerInfoPropsFile>$(MSBuildThisFileDirectory)..\..\build\SomeVerInfo.props</SomeVerInfoPropsFile>
    <SomeVerInfoCsFile>$(MSBuildThisFileDirectory)..\..\build\SomeVerInfo.cs</SomeVerInfoCsFile>
    <SomeVerInfoRcFile>$(MSBuildThisFileDirectory)..\..\build\SomeVerInfo.rc</SomeVerInfoRcFile>
  </PropertyGroup>

  <ItemGroup>
    <VersionTemplate Include="SomeVerInfo.props.pp" OutputPath="$(SomeVerInfoPropsFile)" />
    <VersionTemplate Include="SomeVerInfo.cs.pp" OutputPath="$(SomeVerInfoCsFile)" />
    <VersionTemplate Include="SomeVerInfo.rc.pp" OutputPath="$(SomeVerInfoRcFile)" />
    <VersionTemplate Include="Directory.Packages.props.pp"
                     OutputPath="$(MSBuildThisFileDirectory)..\..\Directory.Packages.props" />
  </ItemGroup>
</Project>
```

The generated props can map generic SomeVer values to repository-specific names:

```xml
<Project>
  <PropertyGroup>
    <SomeVerInfoImported>true</SomeVerInfoImported>
    <SomeVerInfoNextMajor>{nextmajor}</SomeVerInfoNextMajor>
    <SomeVerInfoNextMinor>{nextminor}</SomeVerInfoNextMinor>
    <SomeVerInfoMajor>{major}</SomeVerInfoMajor>
    <SomeVerInfoMinor>{minor}</SomeVerInfoMinor>
    <SomeVerInfoPatch>{patch}</SomeVerInfoPatch>
    <SomeVerInfoHeight>{height}</SomeVerInfoHeight>
    <SomeVerInfoFullHeight>{fullheight}</SomeVerInfoFullHeight>
    <SomeVerInfoLabel>{label}</SomeVerInfoLabel>
    <SomeVerInfoDashLabel>{dashlabel}</SomeVerInfoDashLabel>
    <SomeVerInfoSha>{sha}</SomeVerInfoSha>
    <SomeVerInfoShortSha>{shortsha}</SomeVerInfoShortSha>
    <SomeVerInfoVersion>{informationalversion}</SomeVerInfoVersion>
    <AssemblyVersion>{major}.0.0.0</AssemblyVersion>
    <FileVersion>{fileversion}</FileVersion>
    <InformationalVersion>{informationalversion}</InformationalVersion>
    <PackageVersion>{packageversion}</PackageVersion>
  </PropertyGroup>
</Project>
```

Those generated `SomeVerInfo*` names are convention, not reserved SomeVer outputs. This lets later projects consume one cached version for managed assemblies, native resources, installer sources, manifests, package versions, and custom MSBuild logic. The generated file must still be imported or included by the consuming build.

To version multiple products independently in one repository, give each version project a different prefix. For example, `wix7-v` recognizes `wix7-v1.2.3`, while `wix6-v` recognizes `wix6-v4.5.6`. A shared `.targets` file can hold common configuration and template declarations while each product project sets only its prefix.

## CI guidance

SomeVer reads Git history and tags. Shallow clones will often produce the default version instead of the expected tagged version.

For GitHub Actions, fetch the full history or enough history to reach your tags:

```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0
    filter: tree:0
```

The repository's own workflow uses this configuration in [build.yml](./.github/workflows/build.yml).

## Troubleshooting

If SomeVer appears to ignore a tag, check these first:

- the tag is on the current commit or an ancestor of it
- the tag matches `SomeVerTagPrefix`
- the tag is valid SemVer 2.0 after removing the prefix
- a higher version tag is not reachable from another history path
- the checkout is not shallow
- `SomeVerSkip` is not set to `true`

If a generated file is missing or stale, check that the `VersionTemplate` input exists, every item has `OutputPath` metadata, the output directory is writable, and the generated file is imported or included by the consuming build. Tokens are lowercase and case-sensitive. Template outputs are refreshed whenever the build reaches the template target, even when the input template timestamp is unchanged.

If you need to see what SomeVer is doing, set:

```xml
<PropertyGroup>
  <SomeVerVerbosity>detailed</SomeVerVerbosity>
</PropertyGroup>
```

---

<sub>[Tag](https://thenounproject.com/icon/tag-6800795/) by [metami septiana](https://thenounproject.com/creator/metami/) from [the Noun Project](https://thenounproject.com/) (CC BY 3.0).</sub>
